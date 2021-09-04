using System;

namespace Multiplayer.Compat
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class MpCompatForAttribute : Attribute
    {
        public string PackageId { get; }

        public MpCompatForAttribute(string packageId)
        {
            this.PackageId = packageId;
        }

        public override object TypeId {
            get {
                return this;
            }
        }
    }
}