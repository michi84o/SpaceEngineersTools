namespace SpaceEngineersToolsShared
{
    public class ComplexMaterial
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public override string ToString()
        {
            return Value + " | " + (Name ?? "<?>");
        }
    }
}
