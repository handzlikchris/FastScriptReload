namespace Extensions
{
    public static class CardinalDirectionExtension
    {
        public static CardinalDirection Flip(this CardinalDirection cardinalDirection)
        {
            return cardinalDirection switch
            {
                CardinalDirection.East => CardinalDirection.West,
                CardinalDirection.North => CardinalDirection.South,
                CardinalDirection.West => CardinalDirection.East,
                _ => CardinalDirection.None
            };
        }
    }
}