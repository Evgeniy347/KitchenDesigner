using NUnit.Framework;

/*
  Фикстуры Geometry.Tests идут параллельно. Файл лежит в geometry/tests/, а НЕ в
  Assets/Tests/EditMode/Geometry/ — те же исходники компилирует Unity, и её
  раннер EditMode однопоточный: атрибут внутри Assets достался бы и ему.
  Общие мутируемые буферы ядра расшиты по [ThreadStatic]
  (ValidationBroadPhase, ValidationCore, ContactShadow).
*/
[assembly: Parallelizable(ParallelScope.Fixtures)]
