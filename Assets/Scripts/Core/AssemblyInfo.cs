using System.Runtime.CompilerServices;

// EditMode-тесты дёргают покадровые швы (например, CooktopElement.Update),
// которые в рантайме вызывает цикл Unity, а в EditMode-тестах он не крутится.
[assembly: InternalsVisibleTo("KitchenDesigner.Tests.EditMode")]
