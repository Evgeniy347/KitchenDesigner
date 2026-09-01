using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Реестр и его двойник в контракте расходятся молча
    /// (CONVENTIONS.md → «A capability table has a twin in the contract»).
    /// Здесь двойников двое: интерфейс IElementFactory и статический фасад
    /// ElementFactory, через который к фабрике ходит весь код и все тесты.
    ///
    /// Разошлись они уже: CreatePanel был ЕДИНСТВЕННЫМ методом интерфейса без
    /// обёртки на фасаде, и это молчало годами — Instance объявлен internal, так
    /// что EditMode обходил дыру через него, а PlayMode-сборка, которой
    /// InternalsVisibleTo не выдан, просто не могла создать панель. Пропуск
    /// стоил изометрического снимка ХДФ-задника.
    ///
    /// Расхождение считается дефектом в ОБЕ стороны: метод без обёртки прячет
    /// работающую возможность, обёртка без метода — мёртвый код, который
    /// компилятор не найдёт, потому что фасад делегирует по имени.</summary>
    public class ElementFactoryFacadeTests
    {
        private static readonly string[] Contract =
            { "Assets", "Scripts", "Core", "Interfaces", "IElementFactory.cs" };

        private static readonly string[] Facade =
            { "Assets", "Scripts", "Core", "Elements", "ElementFactory.cs" };

        /// <summary>Тип возврата в шаблонах намеренно не назван: этот каталог
        /// собирается вторым проходом под dotnet, и сторож запрещённых символов
        /// не отличает имя сценового типа в РЕГУЛЯРКЕ от обращения к движку.
        /// Правило держится на «public static … CreateXxx(», а не на типе.</summary>
        private const string ContractPattern = @"^\s+\w+\s+(Create\w+)\s*\(";

        private const string FacadePattern = @"\bpublic\s+static\s+\w+\s+(Create\w+)\s*\(";

        private static SortedSet<string> MethodsIn(string[] path, string pattern)
        {
            var dir = RepoPaths.Subdir(path.Take(path.Length - 1).ToArray());
            var file = Path.Combine(dir, path[path.Length - 1]);
            Assert.IsTrue(File.Exists(file),
                "скан читает не тот путь: по несуществующему файлу он вернёт пустой набор "
                + "и сравнение двух пустот пройдёт, ничего не проверив — " + file);

            var found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var line in SourceLines.CodeOnly(File.ReadAllLines(file)))
                foreach (Match m in Regex.Matches(line, pattern))
                    found.Add(m.Groups[1].Value);
            return found;
        }

        [Test]
        public void EveryFactoryMethod_HasAWrapperOnThePublicFacade()
        {
            var contract = MethodsIn(Contract, ContractPattern);
            var facade = MethodsIn(Facade, FacadePattern);

            var missing = contract.Except(facade).ToList();
            var extra = facade.Except(contract).ToList();

            Assert.IsEmpty(missing,
                "метод фабрики без обёртки на фасаде недоступен снаружи ядра: фасад — "
                + "единственный публичный вход, Instance объявлен internal. Именно так "
                + "PlayMode не мог создать панель. Дописать обёртку:\n"
                + string.Join("\n", missing));

            Assert.IsEmpty(extra,
                "обёртка на фасаде без метода в интерфейсе — мёртвый код, и компилятор его "
                + "не найдёт: фасад делегирует по имени. Убрать с фасада:\n"
                + string.Join("\n", extra));
        }

        [Test]
        public void TheScan_ReadsBothFiles_AndTellsAWrapperFromAPlainMethod()
        {
            var contract = MethodsIn(Contract, ContractPattern);
            var facade = MethodsIn(Facade, FacadePattern);

            CollectionAssert.Contains(contract, "CreatePart",
                "скан интерфейса не нашёл заведомо существующего метода — он читает не то, "
                + "что думает");
            CollectionAssert.Contains(facade, "CreatePart",
                "то же для фасада: без известного имени «ноль находок» не отличить от "
                + "грепа мимо файла");
            Assert.Greater(contract.Count, 10,
                "фабрика заводит два десятка типов: единичная находка означала бы, что "
                + "шаблон совпал случайно");

            Assert.AreEqual(1, Regex.Matches(
                "        public static Thing CreateOven(string name) =>",
                FacadePattern).Count,
                "обёртка фасада — это public static; на ней и стоит правило");
            Assert.AreEqual(0, Regex.Matches(
                "        Thing CreateOven(string name);",
                FacadePattern).Count,
                "объявление интерфейса обёрткой не является: спутав их, тест считал бы "
                + "фасад полным, ни разу в него не заглянув");
            Assert.AreEqual(1, Regex.Matches(
                "        Thing CreateOven(string name);",
                ContractPattern).Count,
                "а для интерфейса — наоборот");
        }
    }
}
