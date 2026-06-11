using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Тесты чистой логики выбора эксперимента (ExperimentSelector).
/// Определения создаются через рефлексию, потому что поля приватны и
/// сериализуются Unity — это позволяет не добавлять тестовые сеттеры в продакшн.
/// </summary>
[TestFixture]
public class ExperimentSelectorTests
{
    private readonly List<ExperimentDefinition> created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (ExperimentDefinition def in created)
        {
            if (def != null) Object.DestroyImmediate(def);
        }
        created.Clear();
    }

    private ExperimentDefinition Def(
        string id, int minDay = 1, int maxDay = 0,
        int minParticipants = 1, int maxParticipants = 4, bool implemented = true)
    {
        var def = ScriptableObject.CreateInstance<ExperimentDefinition>();
        SetField(def, "id", id);
        SetField(def, "displayName", id);
        SetField(def, "category", ExperimentCategory.Solo);
        SetField(def, "sceneName", id);
        SetField(def, "minDay", minDay);
        SetField(def, "maxDay", maxDay);
        SetField(def, "minParticipants", minParticipants);
        SetField(def, "maxParticipants", maxParticipants);
        SetField(def, "implemented", implemented);
        created.Add(def);
        return def;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = typeof(ExperimentDefinition).GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Поле {name} не найдено");
        field.SetValue(target, value);
    }

    [Test]
    public void Select_EmptyPool_ReturnsNull()
    {
        var result = ExperimentSelector.Select(
            new List<ExperimentDefinition>(), 1, 2, new HashSet<string>(), new System.Random(1));
        Assert.IsNull(result);
    }

    [Test]
    public void Select_NullArguments_ReturnNull()
    {
        Assert.IsNull(ExperimentSelector.Select(null, 1, 2, null, new System.Random(1)));
        Assert.IsNull(ExperimentSelector.Select(new List<ExperimentDefinition> { Def("a") }, 1, 2, null, null));
    }

    [Test]
    public void Select_FiltersByDay()
    {
        var pool = new List<ExperimentDefinition> { Def("late", minDay: 3) };
        Assert.IsNull(ExperimentSelector.Select(pool, 1, 2, new HashSet<string>(), new System.Random(1)),
            "Игра с minDay=3 не должна выбираться на дне 1");
        Assert.IsNotNull(ExperimentSelector.Select(pool, 3, 2, new HashSet<string>(), new System.Random(1)));
    }

    [Test]
    public void Select_FiltersByParticipants()
    {
        var pool = new List<ExperimentDefinition> { Def("big", minParticipants: 3) };
        Assert.IsNull(ExperimentSelector.Select(pool, 1, 2, new HashSet<string>(), new System.Random(1)),
            "Игра, требующая 3 участников, не должна выбираться при 2");
    }

    [Test]
    public void Select_ExcludesUnimplemented()
    {
        var pool = new List<ExperimentDefinition> { Def("paper", implemented: false) };
        Assert.IsNull(ExperimentSelector.Select(pool, 1, 2, new HashSet<string>(), new System.Random(1)));
    }

    [Test]
    public void Select_IsDeterministicForSeed()
    {
        var pool = new List<ExperimentDefinition> { Def("a"), Def("b"), Def("c") };
        var first = ExperimentSelector.Select(pool, 1, 2, new HashSet<string>(), new System.Random(42));
        var second = ExperimentSelector.Select(pool, 1, 2, new HashSet<string>(), new System.Random(42));
        Assert.AreEqual(first.Id, second.Id);
    }

    [Test]
    public void Select_AvoidsRepeatsWhileFreshExist()
    {
        var pool = new List<ExperimentDefinition> { Def("a"), Def("b") };
        var played = new HashSet<string> { "a" };
        for (int seed = 0; seed < 20; seed++)
        {
            var result = ExperimentSelector.Select(pool, 1, 2, played, new System.Random(seed));
            Assert.AreEqual("b", result.Id, "Пока есть несыгранная игра, повтор не должен выбираться");
        }
    }

    [Test]
    public void Select_AllPlayed_AllowsRepeat()
    {
        var pool = new List<ExperimentDefinition> { Def("a"), Def("b") };
        var played = new HashSet<string> { "a", "b" };
        var result = ExperimentSelector.Select(pool, 1, 2, played, new System.Random(1));
        Assert.IsNotNull(result, "Когда все сыграны, пул исчерпан и повтор разрешён");
        Assert.That(result.Id, Is.EqualTo("a").Or.EqualTo("b"));
    }

    [Test]
    public void Select_NoAvoidRepeats_PicksFromAll()
    {
        var pool = new List<ExperimentDefinition> { Def("a"), Def("b") };
        var played = new HashSet<string> { "a" };
        // Со снятым avoidRepeats сыгранная игра снова в выборке — за серию seed'ов встретится "a".
        bool sawA = false;
        for (int seed = 0; seed < 50 && !sawA; seed++)
        {
            var result = ExperimentSelector.Select(pool, 1, 2, played, new System.Random(seed), avoidRepeats: false);
            if (result.Id == "a") sawA = true;
        }
        Assert.IsTrue(sawA);
    }
}
