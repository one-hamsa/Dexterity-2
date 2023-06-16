using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class RequiresStateFunctionAttribute : Attribute
    {
    }
}
