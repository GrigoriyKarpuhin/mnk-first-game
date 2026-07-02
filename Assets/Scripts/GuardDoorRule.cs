/// <summary>
/// Правило прохода охраны через дверную клетку. Вынесено чистой логикой, чтобы покрыть
/// юнит-тестами (<see cref="GuardDoorRuleTests"/>). Так охрану больше нельзя запереть в
/// комнате обычной дверью: свободную дверь на пути она открывает сама.
/// </summary>
public static class GuardDoorRule
{
    /// <summary>
    /// Может ли охранник пройти дверную клетку прямо сейчас. В погоне — выламывает любую
    /// дверь; иначе открывает только свободную (проходимую для NPC) и не запечатанную
    /// системой. Голый дверной тайл без объекта двери считаем свободным.
    /// </summary>
    public static bool CanPass(bool isChase, bool hasDoorObject, bool canNpcTraverse, bool isSealed)
    {
        if (isChase) return true;
        if (!hasDoorObject) return true;
        return canNpcTraverse && !isSealed;
    }
}
