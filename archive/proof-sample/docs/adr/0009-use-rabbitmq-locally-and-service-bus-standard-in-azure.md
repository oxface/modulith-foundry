---
status: accepted
---

# Use RabbitMQ locally and Service Bus Standard in Azure

The default local durable-messaging environment uses RabbitMQ through Rebus so acknowledgement, redelivery, poison-message, and dead-letter behavior are tested against a real broker. Azure deployments use Azure Service Bus Standard as the cheapest tier supporting topics and subscriptions. A focused compatibility suite runs against a temporary real Service Bus Standard namespace before an Azure release because changing the Rebus transport does not make topology, limits, scheduling, transactions, or operational failure behavior identical.
