using NUnit.Framework;

/*
  Фикстуры Pure.Tests идут параллельно. Файл лежит в geometry/pure-tests/,
  а НЕ в Assets/Tests/EditMode/Pure/ — те же исходники компилирует Unity,
  и её раннер EditMode однопоточный: атрибут внутри Assets достался бы и ему.
*/
[assembly: Parallelizable(ParallelScope.Fixtures)]
