using System.Collections.Generic;
using KitchenDesigner.Core;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Сырая запись элемента — единственное, чем поле, которого эта версия
    /// не знает, доживает от чтения файла до его записи. `JsonUtility` при разборе
    /// выбрасывает неизвестный ключ молча, поэтому круг «сохранено новой версией →
    /// открыто старой → сохранено старой» терял объект целиком: он превращался в
    /// обычную деталь, и следующее сохранение закрепляло подмену.
    ///
    /// Здесь проверяется механика слияния: известные версии поля берутся из свежего
    /// снимка (объект могли передвинуть), все остальные — из сырой записи, слово в
    /// слово. Порядок ключей тоже держится за сырой записью: иначе диффы файла
    /// пользователя становятся нечитаемыми на ровном месте.</summary>
    public class RawElementRecordsTests
    {
        private const string ProjectWithTwoElements = @"{
    ""version"": 1,
    ""elements"": [
        {
            ""name"": ""A"",
            ""elementType"": ""washer"",
            ""spinRpm"": 1200,
            ""movable"": true
        },
        {
            ""name"": ""B"",
            ""elementType"": ""board"",
            ""movable"": true
        }
    ]
}";

        [Test]
        public void Extract_ReturnsOneRecordPerElement_InFileOrder()
        {
            var records = RawElementRecords.Extract(ProjectWithTwoElements);

            Assert.AreEqual(2, records.Count);
            StringAssert.Contains("\"spinRpm\": 1200", records[0]);
            StringAssert.Contains("\"name\": \"B\"", records[1]);
            StringAssert.DoesNotContain("spinRpm", records[1],
                "запись второго элемента не должна прихватывать поля первого");
        }

        [Test]
        public void Extract_OnProjectWithoutElements_ReturnsNothing()
        {
            Assert.IsEmpty(RawElementRecords.Extract(@"{ ""version"": 1 }"));
            Assert.IsEmpty(RawElementRecords.Extract(""));
        }

        [Test]
        public void Merge_KeepsFieldsThisVersionDoesNotKnow()
        {
            string raw = @"{ ""name"": ""A"", ""spinRpm"": 1200, ""drumLitres"": 65 }";
            string current = @"{ ""name"": ""A"", ""movable"": false }";

            string merged = RawElementRecords.Merge(raw, current, "");

            Assert.AreEqual("1200", MemberOf(merged, "spinRpm"));
            Assert.AreEqual("65", MemberOf(merged, "drumLitres"));
        }

        [Test]
        public void Merge_TakesKnownFieldsFromTheFreshSnapshot()
        {
            string raw = @"{ ""name"": ""A"", ""position"": [ 0.0, 0.0, 0.0 ], ""spinRpm"": 1200 }";
            string current = @"{ ""name"": ""A"", ""position"": [ 1.5, 0.0, 2.0 ] }";

            string merged = RawElementRecords.Merge(raw, current, "");

            Assert.AreEqual("[ 1.5, 0.0, 2.0 ]", MemberOf(merged, "position"),
                "передвинутый объект обязан уехать в файл на НОВОМ месте");
            Assert.AreEqual("1200", MemberOf(merged, "spinRpm"),
                "и при этом не растерять поля, которых эта версия не знает");
        }

        [Test]
        public void Merge_AppendsFieldsTheRawRecordNeverHad()
        {
            string raw = @"{ ""name"": ""A"" }";
            string current = @"{ ""name"": ""A"", ""edgeSuppressedMask"": 3 }";

            Assert.AreEqual("3", MemberOf(RawElementRecords.Merge(raw, current, ""),
                "edgeSuppressedMask"));
        }

        [Test]
        public void Merge_KeepsTheOrderOfTheRawRecord()
        {
            string raw = @"{ ""name"": ""A"", ""spinRpm"": 1200, ""movable"": true }";
            string current = @"{ ""movable"": false, ""name"": ""A"" }";

            var keys = KeysOf(RawElementRecords.Merge(raw, current, ""));

            CollectionAssert.AreEqual(new[] { "name", "spinRpm", "movable" }, keys);
        }

        [Test]
        public void Apply_ReplacesOnlyTheNamedElement()
        {
            string fresh = @"{
    ""version"": 1,
    ""elements"": [
        {
            ""name"": ""A"",
            ""movable"": false
        },
        {
            ""name"": ""B"",
            ""movable"": true
        }
    ]
}";
            var raw = new List<string?>
            {
                @"{ ""name"": ""A"", ""spinRpm"": 1200, ""movable"": true }",
                null,
            };

            string applied = RawElementRecords.Apply(fresh, raw);

            var records = RawElementRecords.Extract(applied);
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual("1200", MemberOf(records[0], "spinRpm"));
            Assert.AreEqual("false", MemberOf(records[0], "movable"),
                "свежее значение известного поля побеждает сырое");
            Assert.AreEqual("true", MemberOf(records[1], "movable"));
            Assert.IsFalse(records[1].Contains("spinRpm"),
                "элемент без сырой записи остаётся ровно таким, каким его написал сериализатор");
        }

        [Test]
        public void Apply_WithoutAnyRawRecord_ChangesNothing()
        {
            Assert.AreEqual(ProjectWithTwoElements,
                RawElementRecords.Apply(ProjectWithTwoElements, new List<string?> { null, null }));
        }

        [Test]
        public void Apply_KeepsTheIndentationOfTheArrayItem()
        {
            string fresh = @"{
    ""elements"": [
        {
            ""name"": ""A""
        }
    ]
}";
            string applied = RawElementRecords.Apply(fresh,
                new List<string?> { @"{ ""name"": ""A"", ""spinRpm"": 1200 }" });

            StringAssert.Contains("\n            \"spinRpm\": 1200", applied,
                "поля элемента живут на третьем уровне отступа, как их пишет сериализатор");
            StringAssert.Contains("\n        }", applied);
        }

        private static string MemberOf(string json, string key)
        {
            var value = JsonText.MemberValue(json, JsonText.RootObject(json), key);
            return value.Found ? value.Text(json) : "";
        }

        private static List<string> KeysOf(string json)
        {
            var keys = new List<string>();
            foreach (var member in JsonText.Members(json, JsonText.RootObject(json)))
                keys.Add(member.Key);
            return keys;
        }
    }
}
