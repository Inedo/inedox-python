using System.Text.Json.Serialization;

namespace Inedo.Extensions.Python.Operations
{
    public sealed class TestCaseID : IEquatable<TestCaseID>
    {
        public static bool operator ==(TestCaseID a, TestCaseID b) => ReferenceEquals(a, b) || (a is not null && a.Equals(b));
        public static bool operator !=(TestCaseID a, TestCaseID b) => !(a == b);

        [JsonIgnore]
        public string Group => this.ID.LastIndexOf('.') == -1 ? null : this.ID[..this.ID.LastIndexOf('.')];
        [JsonIgnore]
        public string Name => this.ID[(this.ID.LastIndexOf('.') + 1)..];

        public string ID { get; set; }
        public string Desc { get; set; }

        public bool Equals(TestCaseID other) => other is not null && this.ID == other.ID;
        public override bool Equals(object obj) => this.Equals(obj as TestCaseID);
        public override int GetHashCode() => this.ID?.GetHashCode() ?? 0;
    }
}
