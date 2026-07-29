namespace KitchenDesigner.Core
{
    /// <summary>Куда именно надевается декор. У обычной детали слот один
    /// (свойство «Текстура»), у стола их три: щит, столешница и ножки — каждый со
    /// своим id в <see cref="TableElement"/>/<see cref="RadiusTableElement"/>.
    ///
    /// Общий тип, а не приватное перечисление окна свойств: через слот ходят
    /// <see cref="MaterialManager.ApplySlot"/>, <see cref="SetMaterialCommand"/>
    /// и пипетка.</summary>
    public enum MaterialSlot
    {
        Base,
        Tabletop,
        Legs,
    }
}
