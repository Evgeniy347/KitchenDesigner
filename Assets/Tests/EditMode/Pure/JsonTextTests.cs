using KitchenDesigner.Core;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Разбор текста JSON без `JsonUtility` — он живёт в
    /// `UnityEngine.JSONSerializeModule` и на быстром пути не компилируется вовсе,
    /// а главное: он отбрасывает неизвестные поля, ради сохранения которых этот
    /// сканер и написан.
    ///
    /// Ловушки, которые тут стерегутся, все встречаются в настоящем файле проекта:
    /// фигурная скобка внутри строкового значения, экранированная кавычка,
    /// вложенные массивы координат и многострочная запись `JsonUtility`.</summary>
    public class JsonTextTests
    {
        [Test]
        public void Members_ReadsKeysAndValues()
        {
            string json = @"{ ""name"": ""A"", ""count"": 3, ""on"": true }";
            var members = JsonText.Members(json, JsonText.RootObject(json));

            Assert.AreEqual(3, members.Count);
            Assert.AreEqual("name", members[0].Key);
            Assert.AreEqual("\"A\"", members[0].Value.Text(json));
            Assert.AreEqual("3", members[1].Value.Text(json));
            Assert.AreEqual("true", members[2].Value.Text(json));
        }

        [Test]
        public void Members_DoesNotTripOnBracesInsideAString()
        {
            string json = @"{ ""name"": ""a } { b"", ""count"": 7 }";

            Assert.AreEqual("7", JsonText
                .MemberValue(json, JsonText.RootObject(json), "count").Text(json));
        }

        [Test]
        public void Members_DoesNotTripOnAnEscapedQuote()
        {
            string json = @"{ ""name"": ""a \"" b"", ""count"": 7 }";

            Assert.AreEqual("7", JsonText
                .MemberValue(json, JsonText.RootObject(json), "count").Text(json));
        }

        [Test]
        public void MemberValue_ReturnsNestedObjectAndArrayWhole()
        {
            string json = @"{ ""a"": { ""b"": [ 1, 2 ] }, ""c"": 9 }";

            Assert.AreEqual(@"{ ""b"": [ 1, 2 ] }",
                JsonText.MemberValue(json, JsonText.RootObject(json), "a").Text(json));
            Assert.AreEqual("9",
                JsonText.MemberValue(json, JsonText.RootObject(json), "c").Text(json));
        }

        [Test]
        public void MemberValue_OnAMissingKey_IsNotFound()
        {
            string json = @"{ ""a"": 1 }";

            Assert.IsFalse(JsonText.MemberValue(json, JsonText.RootObject(json), "b").Found);
        }

        [Test]
        public void ArrayItems_CountsNestedObjects()
        {
            string json = @"[ { ""a"": [ 1, 2 ] }, { ""a"": [] }, 5 ]";
            var items = JsonText.ArrayItems(json, new JsonSpan(0, json.Length));

            Assert.AreEqual(3, items.Count);
            Assert.AreEqual(@"{ ""a"": [] }", items[1].Text(json));
            Assert.AreEqual("5", items[2].Text(json));
        }

        [Test]
        public void RootObject_OnTextThatIsNotAnObject_IsNotFound()
        {
            Assert.IsFalse(JsonText.RootObject("[ 1 ]").Found);
            Assert.IsFalse(JsonText.RootObject("").Found);
        }

        [Test]
        public void LineIndentBefore_ReportsOnlyTheLeadingWhitespaceOfThatLine()
        {
            string json = "{\n    \"a\": 1\n}";
            int keyIndex = json.IndexOf("\"a\"", System.StringComparison.Ordinal);

            Assert.AreEqual("    ", JsonText.LineIndentBefore(json, keyIndex));
        }

        [Test]
        public void LineIndentBefore_WhenSomethingElseStandsOnTheLine_IsEmpty()
        {
            string json = "{ \"a\": 1 }";
            int valueIndex = json.IndexOf("1", System.StringComparison.Ordinal);

            Assert.AreEqual("", JsonText.LineIndentBefore(json, valueIndex));
        }
    }
}
