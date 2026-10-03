using System.Threading.Channels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.ExtensionPoints;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Composition;
using ModulithFoundry.Modules.Sales.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

internal sealed class SalesApprovalFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private ServiceProvider _services = null!;
    private IHostedService[] _workers = [];
    internal OrganizationAccessContext Administrator { get; private set; } = null!;
    internal OrganizationAccessContext Approver { get; private set; } = null!;
    private StockItemId _item;

    internal static async Task<SalesApprovalFixture> CreateAsync()
    {
        var fixture = new SalesApprovalFixture();
        try
        {
            await fixture.InitializeAsync();
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private async Task InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton(NpgsqlDataSource.Create(_postgres.GetConnectionString()));
        services.AddScoped<ActorContextAccessor>();
        services.AddScoped<IOrganizationContextAccessor>(provider =>
            provider.GetRequiredService<ActorContextAccessor>()
        );
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Invitations:PublicApplicationUrl"] = "https://example.test",
                    ["Email:Smtp:Host"] = "localhost",
                    ["Email:Smtp:Port"] = "1025",
                    ["Email:Smtp:Security"] = "None",
                    ["Email:Smtp:FromAddress"] = "no-reply@example.test",
                }
            )
            .Build();
        services.AddAccessModule(configuration);
        services.AddSingleton<CapturedEmailTransport>();
        services.Replace(
            ServiceDescriptor.Singleton<IEmailTransport>(provider =>
                provider.GetRequiredService<CapturedEmailTransport>()
            )
        );
        services.AddInventoryModule();
        services.AddSalesModule();
        _services = services.BuildServiceProvider(validateScopes: true);
        await _services.MigrateAccessAsync(TestContext.Current.CancellationToken);
        await _services.MigrateInventoryAsync(TestContext.Current.CancellationToken);
        await _services.MigrateSalesAsync(TestContext.Current.CancellationToken);
        Administrator = await CreateOrganizationAsync("approval");
        _workers = [.. _services.GetServices<IHostedService>()];
        foreach (IHostedService worker in _workers)
            await worker.StartAsync(TestContext.Current.CancellationToken);
        Approver = await InviteAsync("approver", [SalesRoleIds.Approver]);
        await RunAsync(
            Administrator,
            async provider =>
            {
                Assert.IsType<CreateCustomerResult.Created>(
                    await provider
                        .GetRequiredService<ICustomerAdministration>()
                        .CreateAsync(
                            new(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                "buyer",
                                "Buyer"
                            ),
                            TestContext.Current.CancellationToken
                        )
                );
                _item = Assert
                    .IsType<CreateStockItemResult.Created>(
                        await provider
                            .GetRequiredService<IStockItemAdministration>()
                            .CreateAsync(
                                new(
                                    Administrator.UserId,
                                    Administrator.OrganizationId,
                                    "bolt",
                                    "Bolt",
                                    "ea"
                                ),
                                TestContext.Current.CancellationToken
                            )
                    )
                    .Item.StockItemId;
            }
        );
    }

    internal async Task<OrganizationAccessContext> CreateOrganizationAsync(string slug)
    {
        return await RunAsync(
            null,
            async provider =>
            {
                UserIdentityLink user = await provider
                    .GetRequiredService<IExternalIdentityLinking>()
                    .LinkAsync(
                        ExternalIdentity.Create(
                            "https://issuer.example",
                            slug,
                            slug + "@example.test",
                            slug
                        ),
                        TestContext.Current.CancellationToken
                    );
                Assert.IsType<CreateOrganizationResult.Created>(
                    await provider
                        .GetRequiredService<IOrganizationCreation>()
                        .CreateOrganizationAsync(
                            new(user.UserId, slug, slug),
                            TestContext.Current.CancellationToken
                        )
                );
                var queries = provider.GetRequiredService<IOrganizationQueries>();
                OrganizationAccessContext context = (
                    await queries.ResolveAccessAsync(
                        user.UserId,
                        slug,
                        TestContext.Current.CancellationToken
                    )
                )!;
                provider.GetRequiredService<ActorContextAccessor>().OrganizationContext = context;
                Assert.IsType<ReplaceMembershipRolesResult.Updated>(
                    await provider
                        .GetRequiredService<IOrganizationMembershipAdministration>()
                        .ReplaceRolesAsync(
                            new(
                                context.UserId,
                                context.OrganizationId,
                                context.MembershipId,
                                [
                                    SystemRoleIds.OrganizationAdministrator,
                                    SalesRoleIds.Manager,
                                    SalesRoleIds.Clerk,
                                    InventoryRoleIds.Manager,
                                ]
                            ),
                            TestContext.Current.CancellationToken
                        )
                );
                return (
                    await queries.ResolveAccessAsync(
                        user.UserId,
                        slug,
                        TestContext.Current.CancellationToken
                    )
                )!;
            }
        );
    }

    internal Task<OrganizationAccessContext> InviteAsync(
        string subject,
        IReadOnlyCollection<string> roles
    ) =>
        RunAsync(
            Administrator,
            async provider =>
            {
                string email = subject + "@example.test";
                var operations = provider.GetRequiredService<IOrganizationInvitationOperations>();
                InvitationId invitationId = Assert
                    .IsType<CreateOrganizationInvitationResult.Created>(
                        await operations.CreateInvitationAsync(
                            new(Administrator.UserId, Administrator.OrganizationId, email, roles),
                            TestContext.Current.CancellationToken
                        )
                    )
                    .Invitation.InvitationId;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                    TestContext.Current.CancellationToken
                );
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                EmailMessage message = await _services
                    .GetRequiredService<CapturedEmailTransport>()
                    .ReadAsync(timeout.Token);
                const string prefix = "Accept the invitation: ";
                var uri = new Uri(
                    message
                        .TextBody.Split('\n', StringSplitOptions.TrimEntries)
                        .Single(line => line.StartsWith(prefix, StringComparison.Ordinal))[
                        prefix.Length..
                    ]
                );
                string secret = uri
                    .Query.TrimStart('?')
                    .Split('&')
                    .Select(part => part.Split('=', 2))
                    .Where(part => part[0] == "code")
                    .Select(part => Uri.UnescapeDataString(part[1]))
                    .Single();
                UserIdentityLink user = await provider
                    .GetRequiredService<IExternalIdentityLinking>()
                    .LinkAsync(
                        ExternalIdentity.Create("https://issuer.example", subject, email, subject),
                        TestContext.Current.CancellationToken
                    );
                Assert.IsType<AcceptOrganizationInvitationResult.Accepted>(
                    await operations.AcceptInvitationAsync(
                        new(user.UserId, invitationId, secret, email),
                        TestContext.Current.CancellationToken
                    )
                );
                return (
                    await provider
                        .GetRequiredService<IOrganizationQueries>()
                        .ResolveAccessAsync(
                            user.UserId,
                            Administrator.OrganizationSlug,
                            TestContext.Current.CancellationToken
                        )
                )!;
            }
        );

    internal Task<SalesOrderView> CreateOrderAsync(string currency = "USD", bool submit = true) =>
        RunAsync(
            Administrator,
            async provider =>
            {
                var operations = provider.GetRequiredService<ISalesOrderOperations>();
                SalesOrderView draft = Assert
                    .IsType<CreateDraftSalesOrderResult.Created>(
                        await operations.CreateDraftAsync(
                            new(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                "buyer",
                                currency,
                                [new(_item, 2m, 25m)]
                            ),
                            TestContext.Current.CancellationToken
                        )
                    )
                    .Order;
                return submit
                    ? Assert
                        .IsType<SubmitSalesOrderResult.Submitted>(
                            await operations.SubmitAsync(
                                new(
                                    Administrator.UserId,
                                    Administrator.OrganizationId,
                                    draft.OrderNumber,
                                    draft.Version
                                ),
                                TestContext.Current.CancellationToken
                            )
                        )
                        .Order
                    : draft;
            }
        );

    internal Task<SetSalesApprovalAuthorityResult> GrantAsync(
        decimal maximum = 50m,
        string currency = "USD",
        long version = 0,
        OrganizationAccessContext? target = null
    ) =>
        RunAsync(
            Administrator,
            provider =>
                provider
                    .GetRequiredService<ISalesApprovalAuthorityAdministration>()
                    .SetAsync(
                        new(
                            Administrator.UserId,
                            Administrator.OrganizationId,
                            (target ?? Approver).MembershipId,
                            maximum,
                            currency,
                            version
                        ),
                        TestContext.Current.CancellationToken
                    )
        );

    internal async Task<T> RunAsync<T>(
        OrganizationAccessContext? actor,
        Func<IServiceProvider, Task<T>> operation
    )
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ActorContextAccessor>().OrganizationContext =
            actor;
        return await operation(scope.ServiceProvider);
    }

    internal async Task RunAsync(
        OrganizationAccessContext? actor,
        Func<IServiceProvider, Task> operation
    )
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ActorContextAccessor>().OrganizationContext =
            actor;
        await operation(scope.ServiceProvider);
    }

    internal Task<NpgsqlConnection> OpenConnectionAsync() =>
        _services
            .GetRequiredService<NpgsqlDataSource>()
            .OpenConnectionAsync(TestContext.Current.CancellationToken)
            .AsTask();

    public async ValueTask DisposeAsync()
    {
        foreach (IHostedService worker in _workers.Reverse())
            await worker.StopAsync(CancellationToken.None);
        if (_services is not null)
            await _services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed class ActorContextAccessor : IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext { get; set; }
    }

    private sealed class CapturedEmailTransport : IEmailTransport
    {
        private readonly Channel<EmailMessage> _messages = Channel.CreateUnbounded<EmailMessage>();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            _messages.Writer.TryWrite(message);
            return Task.CompletedTask;
        }

        internal Task<EmailMessage> ReadAsync(CancellationToken cancellationToken) =>
            _messages.Reader.ReadAsync(cancellationToken).AsTask();
    }
}
