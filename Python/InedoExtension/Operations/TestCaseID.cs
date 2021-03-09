using System.Collections.Generic;
using Newtonsoft.Json;

namespace Inedo.Extensions.Python.Operations
{
    public sealed class TestCaseID
    {
        [JsonIgnore]
        public string Group => this.ID.LastIndexOf('.') == -1 ? null : this.ID.Substring(0, this.ID.LastIndexOf('.'));
        [JsonIgnore]
        public string Name => this.ID.Substring(this.ID.LastIndexOf('.') + 1);

        public string ID { get; set; }
        public string Desc { get; set; }

        public override bool Equals(object obj) => obj != null && this.ID == (obj as TestCaseID)?.ID;
        public override int GetHashCode() => EqualityComparer<string>.Default.GetHashCode(ID);
        public static bool operator ==(TestCaseID case1, TestCaseID case2) => EqualityComparer<TestCaseID>.Default.Equals(case1, case2);
        public static bool operator !=(TestCaseID case1, TestCaseID case2) => !(case1 == case2);
    }
}
