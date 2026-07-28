namespace KitchenDesigner.Core
{
    /// <summary>Счётчик изменений сцены. Любая система, которой раньше пришлось бы
    /// пересчитывать своё состояние каждый кадр, вместо этого запоминает версию и
    /// работает только когда версия сдвинулась.
    ///
    /// Почему счётчик, а не события: Unity не даёт сигнала «содержимое сцены
    /// изменилось» в рантайме (SceneManager.sceneLoaded — про загрузку сцены-ассета,
    /// ObjectChangeEvents — только редактор), а событийная рассылка требует Publish
    /// в каждом мутирующем пути: создание, удаление, перемещение, ресайз, свойства,
    /// MCP, undo, загрузка проекта. Пропустишь один — получишь молча устаревший UI.
    /// Версию же двигают три узких места, мимо которых изменение пройти не может:
    /// реестр (<see cref="PartRegistryInstance"/>), <c>ApplyDimensions</c> и
    /// <see cref="SceneChangeTracker"/> (позы по <c>Transform.hasChanged</c>).</summary>
    public static class SceneRevision
    {
        /// <summary>Монотонно растущая версия состояния сцены.</summary>
        public static int Version { get; private set; }

        public static void Bump() => Version++;

        /// <summary>Сдвинулась ли версия с прошлого опроса. Типичное применение:
        /// <c>if (!SceneRevision.Changed(ref _seenRevision)) return;</c></summary>
        public static bool Changed(ref int seen)
        {
            if (seen == Version) return false;
            seen = Version;
            return true;
        }

        /// <summary>Только для тестов: вернуть счётчик в исходное состояние.</summary>
        public static void Reset() => Version = 0;
    }
}
