namespace NativeOrchestrator.Tests;

public sealed class InMemorySagaStoreContractTests : SagaStoreContractTests
{
  protected override Task<(ISagaRepository Repository, ISagaWorkQueue Queue)> CreateStoreAsync()
  {
    var store = new InMemorySagaStore();
    return Task.FromResult<(ISagaRepository, ISagaWorkQueue)>((store, store));
  }
}
