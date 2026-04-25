namespace OrderFlow.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection that lets every integration test class share a single
/// <see cref="PostgresContainerFixture"/>. Sharing keeps the suite fast —
/// the container starts once even though tests run sequentially in this
/// collection (xUnit serialises tests within a collection).
/// </summary>
[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;
