using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>D9: фоторежим — вид поверх обычного/помещения, а не третье
/// равноправное состояние цикла. <see cref="EditModeManager.LastNonPhotoMode"/> —
/// та база, на которую опирается сегментированный переключатель тулбара, и она
/// обязана переживать вход и выход из фото без искажений.</summary>
public class EditModeManagerTests
{
    [SetUp]
    public void SetUp() => EditModeManager.SetMode(EditMode.Normal);

    [TearDown]
    public void TearDown() => EditModeManager.SetMode(EditMode.Normal);

    [Test]
    public void LastNonPhotoMode_FollowsRoomAndNormal_ButPhotoDoesNotOverwriteIt()
    {
        EditModeManager.SetMode(EditMode.Room);
        Assert.AreEqual(EditMode.Room, EditModeManager.LastNonPhotoMode,
            "переключение в «Помещение» обязано запомниться как текущая база");

        EditModeManager.SetMode(EditMode.Photo);
        Assert.AreEqual(EditMode.Room, EditModeManager.LastNonPhotoMode,
            "вход в фоторежим не должен переписывать базу — иначе сегментированный "
            + "переключатель «Обычный/Помещение» забудет, из какого режима пришёл пользователь");

        EditModeManager.SetMode(EditMode.Normal);
        Assert.AreEqual(EditMode.Normal, EditModeManager.LastNonPhotoMode,
            "а обратное переключение в «Обычный» снова обновляет базу");
    }

    [Test]
    public void SwitchingBetweenNormalAndRoom_NeverTouchesPhoto()
    {
        EditModeManager.SetMode(EditMode.Room);
        Assert.IsFalse(PhotoMode.Active, "«Помещение» само по себе — не фоторежим");

        EditModeManager.SetMode(EditMode.Normal);
        Assert.IsFalse(PhotoMode.Active, "и «Обычный» тоже");
    }

    [Test]
    public void FromPhoto_OneSetModeCall_ReturnsDirectlyToTheRequestedBase()
    {
        EditModeManager.SetMode(EditMode.Room);
        EditModeManager.SetMode(EditMode.Photo);
        Assume.That(PhotoMode.Active, Is.True, "фоторежим должен быть включён после входа");

        EditModeManager.SetMode(EditMode.Normal);

        Assert.IsFalse(PhotoMode.Active,
            "раньше цикл требовал сначала попасть в «Помещение», чтобы выйти из фото — "
            + "теперь один SetMode(Normal) обязан выключить фото и переключить режим сразу");
        Assert.AreEqual(EditMode.Normal, EditModeManager.Mode);
    }
}
