namespace NativeOrchestrator;

/// <summary>
/// Every registered schedule. Building it checks that each one starts a registered saga with the right data type, so a
/// mistake fails at startup (the scheduler depends on the registry).
/// </summary>
internal sealed class SagaScheduleRegistry
{
  public SagaScheduleRegistry(IEnumerable<SagaScheduleRegistration> registrations, SagaRegistry sagas)
  {
    var schedules = new List<SagaSchedule>();
    foreach (var registration in registrations)
    {
      if (!sagas.TryGet(registration.SagaType, out var runtime))
        throw new InvalidOperationException($"Schedule '{registration.Name}' starts saga '{registration.SagaType}', which isn't registered. Register it with AddSaga or fix the saga name in AddSchedule.");
      if (runtime.DataType != registration.DataType)
        throw new InvalidOperationException($"Schedule '{registration.Name}' creates {registration.DataType.Name} data, but saga '{registration.SagaType}' takes {runtime.DataType.Name}. Change the type argument of AddSchedule.");
      schedules.Add(new SagaSchedule(registration));
    }

    Schedules = schedules;
  }

  public IReadOnlyList<SagaSchedule> Schedules { get; }
}
