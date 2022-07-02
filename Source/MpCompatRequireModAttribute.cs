using System;

namespace Multiplayer.Compat
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class MpCompatRequireModAttribute : Attribute
    {
        public string PackageId { get; }

        public MpCompatRequireModAttribute(string packageId) => PackageId = packageId;

        public override object TypeId => this;
    }
}