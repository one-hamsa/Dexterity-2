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

    public static class IExternalMaterialControlFactory
    {
        public delegate IExternalMaterialControl GenerateExternalMaterialControlDelegate(Renderer renderer);

        static private GenerateExternalMaterialControlDelegate _factory;

        public static IExternalMaterialControl GenerateExternalMaterialControl(Renderer renderer)
        {
            if (_factory == null)
            {
                return null;
            }

            return _factory(renderer);
        }

        public static void SetFactory(GenerateExternalMaterialControlDelegate factory)
        {
            _factory = factory;
        }
    }
}
