using Microsoft.Extensions.Options;

namespace NativeOrchestrator.Data.SqlServer;

internal sealed class SqlServerSagaStoreOptionsValidator : IValidateOptions<SqlServerSagaStoreOptions>
{
  public ValidateOptionsResult Validate(string? name, SqlServerSagaStoreOptions options)
  {
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(options.ConnectionString))
      errors.Add("The SQL Server saga store needs a connection string (for example ConnectionStrings:Sagas).");
    if (options.CommandTimeout <= TimeSpan.Zero)
      errors.Add("SqlServerSagaStoreOptions.CommandTimeout must be positive.");
    if (options.MaxRetryCount < 0)
      errors.Add("SqlServerSagaStoreOptions.MaxRetryCount must not be negative.");
    if (options.MaxRetryDelay < TimeSpan.Zero)
      errors.Add("SqlServerSagaStoreOptions.MaxRetryDelay must not be negative.");
    return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
  }
}
