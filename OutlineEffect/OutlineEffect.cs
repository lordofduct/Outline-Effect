/*
//  Copyright (c) 2015 José Guerreiro. All rights reserved.
//
//  MIT license, see http://www.opensource.org/licenses/mit-license.php
//  
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//  
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
//  
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
*/

using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.VR;

namespace com.cakeslice
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode]
    public class OutlineEffect : MonoBehaviour
    {

        public enum OutlinePreset
        {
            A = 0,
            B = 1,
            C = 2
        }

        #region Fields

        [Range(1.0f, 6.0f)]
        public float lineThickness = 1.25f;
        [Range(0, 10)]
        public float lineIntensity = .5f;
        [Range(0, 1)]
        public float fillAmount = 0.2f;

        [UnityEngine.Serialization.FormerlySerializedAs("lineColor0")]
        public Color lineColorA = Color.red;
        [UnityEngine.Serialization.FormerlySerializedAs("lineColor1")]
        public Color lineColorB = Color.green;
        [UnityEngine.Serialization.FormerlySerializedAs("lineColor2")]
        public Color lineColorC = Color.blue;

        public bool additiveRendering = false;

        public bool backfaceCulling = true;

        [Header("These settings can affect performance!")]
        public bool cornerOutlines = false;
        public bool addLinesBetweenColors = false;

        [Header("Advanced settings")]
        public bool scaleWithScreenSize = true;
        [Range(0.1f, .9f)]
        public float alphaCutoff = .5f;
        public bool flipY = false;
        public Camera sourceCamera;




        private Material _outline1Material;
        private Material _outline2Material;
        private Material _outline3Material;
        private Material _outlineEraseMaterial;
        private Shader _outlineShader;
        private Shader _outlineBufferShader;
        private Material _outlineShaderMaterial;
        private RenderTexture _renderTexture;
        private RenderTexture _extraRenderTexture;

        private Dictionary<int, Material> _materialBuffer = new Dictionary<int, Material>();

        #endregion

        #region CONSTRUCTOR

        void Start()
        {
            CreateMaterialsIfNeeded();
            UpdateMaterialsPublicProperties();

            if (sourceCamera == null)
            {
                sourceCamera = this.GetComponent<Camera>();

                if (sourceCamera == null)
                    sourceCamera = Camera.main;
            }

            _renderTexture = new RenderTexture(sourceCamera.pixelWidth, sourceCamera.pixelHeight, 16, RenderTextureFormat.Default);
            _extraRenderTexture = new RenderTexture(sourceCamera.pixelWidth, sourceCamera.pixelHeight, 16, RenderTextureFormat.Default);
        }

        void OnDestroy()
        {
            if (_renderTexture != null)
                _renderTexture.Release();
            if (_extraRenderTexture != null)
                _extraRenderTexture.Release();
            DestroyMaterials();

            OutlineCamera.DestroyCamera();
        }

        #endregion

        #region Methods

        Material GetMaterialFromID(OutlinePreset id)
        {
            switch (id)
            {
                case OutlinePreset.A:
                    return _outline1Material;
                case OutlinePreset.B:
                    return _outline2Material;
                case OutlinePreset.C:
                    return _outline3Material;
                default:
                    return _outline1Material;
            }
        }

        Material CreateMaterial(Color emissionColor)
        {
            Material m = new Material(_outlineBufferShader);
            m.SetColor("_Color", emissionColor);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            return m;
        }

        private void CreateMaterialsIfNeeded()
        {
            if (_outlineShader == null)
                _outlineShader = Resources.Load<Shader>("CakesliceOutlineShader");
            if (_outlineBufferShader == null)
            {
                _outlineBufferShader = Resources.Load<Shader>("CakesliceOutlineBufferShader");
            }
            if (_outlineShaderMaterial == null)
            {
                _outlineShaderMaterial = new Material(_outlineShader);
                _outlineShaderMaterial.hideFlags = HideFlags.HideAndDontSave;
                UpdateMaterialsPublicProperties();
            }
            if (_outlineEraseMaterial == null)
                _outlineEraseMaterial = CreateMaterial(new Color(0, 0, 0, 0));
            if (_outline1Material == null)
                _outline1Material = CreateMaterial(new Color(1, 0, 0, 0));
            if (_outline2Material == null)
                _outline2Material = CreateMaterial(new Color(0, 1, 0, 0));
            if (_outline3Material == null)
                _outline3Material = CreateMaterial(new Color(0, 0, 1, 0));
        }

        private void DestroyMaterials()
        {
            foreach (var p in _materialBuffer)
                DestroyImmediate(p.Value);
            _materialBuffer.Clear();
            DestroyImmediate(_outlineShaderMaterial);
            DestroyImmediate(_outlineEraseMaterial);
            DestroyImmediate(_outline1Material);
            DestroyImmediate(_outline2Material);
            DestroyImmediate(_outline3Material);
            _outlineShader = null;
            _outlineBufferShader = null;
            _outlineShaderMaterial = null;
            _outlineEraseMaterial = null;
            _outline1Material = null;
            _outline2Material = null;
            _outline3Material = null;
        }

        public void UpdateMaterialsPublicProperties()
        {
            if (_outlineShaderMaterial)
            {
                float scalingFactor = 1;
                if (scaleWithScreenSize)
                {
                    // If Screen.height gets bigger, outlines gets thicker
                    scalingFactor = Screen.height / 360.0f;
                }

                // If scaling is too small (height less than 360 pixels), make sure you still render the outlines, but render them with 1 thickness
                float scale = (scaleWithScreenSize && scalingFactor < 1) ? 1f : scalingFactor * lineThickness;
#if UNITY_5
                if(VRSettings.isDeviceActive && sourceCamera.stereoTargetEye != StereoTargetEyeMask.None)
                {
                    _outlineShaderMaterial.SetFloat("_LineThicknessX", scale / VRSettings.eyeTextureWidth);
                    _outlineShaderMaterial.SetFloat("_LineThicknessY", scale / VRSettings.eyeTextureHeight);
#else
                if (UnityEngine.XR.XRSettings.isDeviceActive && sourceCamera.stereoTargetEye != StereoTargetEyeMask.None)
                {
                    _outlineShaderMaterial.SetFloat("_LineThicknessX", scale / UnityEngine.XR.XRSettings.eyeTextureWidth);
                    _outlineShaderMaterial.SetFloat("_LineThicknessY", scale / UnityEngine.XR.XRSettings.eyeTextureHeight);
#endif
                }
                else
                {
                    _outlineShaderMaterial.SetFloat("_LineThicknessX", scale / Screen.width);
                    _outlineShaderMaterial.SetFloat("_LineThicknessY", scale / Screen.height);
                }

                _outlineShaderMaterial.SetFloat("_LineIntensity", lineIntensity);
                _outlineShaderMaterial.SetFloat("_FillAmount", fillAmount);
                _outlineShaderMaterial.SetColor("_LineColor1", lineColorA * lineColorA);
                _outlineShaderMaterial.SetColor("_LineColor2", lineColorB * lineColorB);
                _outlineShaderMaterial.SetColor("_LineColor3", lineColorC * lineColorC);
                if (flipY)
                    _outlineShaderMaterial.SetInt("_FlipY", 1);
                else
                    _outlineShaderMaterial.SetInt("_FlipY", 0);
                if (!additiveRendering)
                    _outlineShaderMaterial.SetInt("_Dark", 1);
                else
                    _outlineShaderMaterial.SetInt("_Dark", 0);
                if (cornerOutlines)
                    _outlineShaderMaterial.SetInt("_CornerOutlines", 1);
                else
                    _outlineShaderMaterial.SetInt("_CornerOutlines", 0);

                Shader.SetGlobalFloat("_OutlineAlphaCutoff", alphaCutoff);
            }
        }

        #endregion

        #region Messages

        public void OnPreRender()
        {
            CreateMaterialsIfNeeded();

            if (_renderTexture == null || _renderTexture.width != sourceCamera.pixelWidth || _renderTexture.height != sourceCamera.pixelHeight)
            {
                _renderTexture = new RenderTexture(sourceCamera.pixelWidth, sourceCamera.pixelHeight, 16, RenderTextureFormat.Default);
                _extraRenderTexture = new RenderTexture(sourceCamera.pixelWidth, sourceCamera.pixelHeight, 16, RenderTextureFormat.Default);
            }
            UpdateMaterialsPublicProperties();
            var cam = OutlineCamera.GetCamera();
            cam.Sync(sourceCamera, _renderTexture);
            cam.CommandBuffer.SetRenderTarget(_renderTexture);

            cam.CommandBuffer.Clear();
            if (Outline.Pool.Count > 0)
            {
                foreach (Outline outline in Outline.Pool)
                {
                    LayerMask l = sourceCamera.cullingMask;

                    if (outline != null && l == (l | (1 << outline.gameObject.layer)))
                    {
                        var arr = outline.GetMaterials();
                        for (int v = 0; v < arr.Length; v++)
                        {
                            Material m = null;

                            if (arr[v] && arr[v].mainTexture != null)
                            {
                                int id = arr[v].mainTexture.GetInstanceID();
                                if (!_materialBuffer.TryGetValue(id, out m))
                                {
                                    if (outline.eraseRenderer)
                                        m = new Material(_outlineEraseMaterial);
                                    else
                                        m = new Material(GetMaterialFromID(outline.presetColor));
                                    m.mainTexture = arr[v].mainTexture;
                                    _materialBuffer[id] = m;
                                }
                            }
                            else
                            {
                                if (outline.eraseRenderer)
                                    m = _outlineEraseMaterial;
                                else
                                    m = GetMaterialFromID(outline.presetColor);
                            }

                            if (backfaceCulling)
                                m.SetInt("_Culling", (int)UnityEngine.Rendering.CullMode.Back);
                            else
                                m.SetInt("_Culling", (int)UnityEngine.Rendering.CullMode.Off);

                            cam.CommandBuffer.DrawRenderer(outline.Renderer, m, 0, 0);
                            if (outline.MeshFilter)
                            {
                                if (outline.MeshFilter.sharedMesh != null)
                                {
                                    for (int i = 1; i < outline.MeshFilter.sharedMesh.subMeshCount; i++)
                                        cam.CommandBuffer.DrawRenderer(outline.Renderer, m, i, 0);
                                }
                            }
                            if (outline.SkinnedMeshRenderer)
                            {
                                if (outline.SkinnedMeshRenderer.sharedMesh != null)
                                {
                                    for (int i = 1; i < outline.SkinnedMeshRenderer.sharedMesh.subMeshCount; i++)
                                        cam.CommandBuffer.DrawRenderer(outline.Renderer, m, i, 0);
                                }
                            }
                        }
                    }
                }
            }

            cam.Camera.Render();
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            _outlineShaderMaterial.SetTexture("_OutlineSource", _renderTexture);

            if (addLinesBetweenColors)
            {
                Graphics.Blit(source, _extraRenderTexture, _outlineShaderMaterial, 0);
                _outlineShaderMaterial.SetTexture("_OutlineSource", _extraRenderTexture);
            }
            Graphics.Blit(source, destination, _outlineShaderMaterial, 1);
        }

        #endregion





        #region Special Types

        private class OutlineCamera : MonoBehaviour
        {

            #region Singleton Interface

            private static OutlineCamera _instance;

            public static OutlineCamera GetCamera()
            {
                if (_instance == null)
                {
                    _instance = (new GameObject("Outline Camera")).AddComponent<OutlineCamera>();
                }
                return _instance;
            }

            public static void DestroyCamera()
            {
                if(_instance != null)
                {
                    Object.Destroy(_instance);
                    _instance = null;
                }
            }

            #endregion

            #region Fields

            public Camera Camera;
            public CommandBuffer CommandBuffer;

            #endregion

            #region CONSTRUCTOR

            private void Awake()
            {
                this.Camera = this.gameObject.AddComponent<Camera>();
                this.Camera.enabled = false;

                this.CommandBuffer = new CommandBuffer();
                this.Camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, this.CommandBuffer);
            }

            #endregion

            #region Methods

            public void Sync(Camera source, RenderTexture texture)
            {
                this.Camera.CopyFrom(source);
                this.Camera.transform.position = source.transform.position;
                this.Camera.transform.rotation = source.transform.rotation;
                this.Camera.renderingPath = RenderingPath.Forward;
                this.Camera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
                this.Camera.clearFlags = CameraClearFlags.SolidColor;
                this.Camera.rect = new Rect(0, 0, 1, 1);
                this.Camera.cullingMask = 0;
                this.Camera.targetTexture = texture;
                this.Camera.enabled = false;
#if UNITY_5_6_OR_NEWER
                this.Camera.allowHDR = false;
#else
                this.Camera.hdr = false;
#endif
            }

            #endregion

        }

        #endregion

    }
}