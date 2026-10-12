namespace RoomMatch.Core.RoomDictionary
{
    internal class ThemeAttribute : Attribute
    {
        private readonly string v;

        public ThemeAttribute(string v)
        {
            this.v = v;
        }
    }
}
