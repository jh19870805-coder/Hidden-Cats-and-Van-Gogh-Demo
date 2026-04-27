namespace HiddenCats.Core
{
    /// <summary>
    /// Types of collectible items in the game.
    /// </summary>
    public enum CollectibleType
    {
        /// <summary>
        /// Normal cat (普通猫)
        /// </summary>
        NormalCat = 0,

        /// <summary>
        /// Hidden cat (隐藏猫)
        /// </summary>
        HiddenCat = 1,

        /// <summary>
        /// Fish (鱼)
        /// </summary>
        Fish = 2,

        /// <summary>
        /// Firework (烟花) - Only exists in CafeWnd scene
        /// </summary>
        Firework = 3
    }
}
