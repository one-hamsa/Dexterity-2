using System.Collections.Generic;

namespace OneHamsa.Dexterity
{
    public interface ISupportPropertyFreeze
    {
        void FreezeProperty(Modifier.PropertyBase property);
    }
}
