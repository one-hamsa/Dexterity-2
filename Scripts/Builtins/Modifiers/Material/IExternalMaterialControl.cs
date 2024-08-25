using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public interface IExternalMaterialControl
    {
        public void SetInt(int kvKey, int kvValue);
        public void SetFloat(int kvKey, float kvValue);
        public void SetVector(int kvKey, Vector4 kvValue);
        public void SetColor(int kvKey, Color kvValue);
        public void SetTexture(int kvKey, Texture kvValue);
        public void SetMatrix(int kvKey, Matrix4x4 kvValue);
    }
}
