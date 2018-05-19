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

namespace com.cakeslice
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Renderer))]
    public class Outline : MonoBehaviour
    {

        #region Multiton Pool

        public static readonly HashSet<Outline> Pool = new HashSet<Outline>();

        #endregion

        #region Fields

        [UnityEngine.Serialization.FormerlySerializedAs("color")]
        public OutlineEffect.OutlinePreset presetColor;
        public bool eraseRenderer;

        [System.NonSerialized]
        private Material[] _materials;

        #endregion

        #region CONSTRUCTOR

        private void Awake()
        {
            this.Renderer = this.GetComponent<Renderer>();
            this.SkinnedMeshRenderer = this.GetComponent<SkinnedMeshRenderer>();
            this.MeshFilter = this.GetComponent<MeshFilter>();
        }

        void OnEnable()
        {
            Pool.Add(this);
        }

        void OnDisable()
        {
            Pool.Remove(this);
        }

        #endregion

        #region Properties

        public Renderer Renderer { get; private set; }

        public SkinnedMeshRenderer SkinnedMeshRenderer { get; private set; }

        public MeshFilter MeshFilter { get; private set; }

        #endregion

        #region Methods

        public void ClearMaterialCache()
        {
            _materials = null;
        }

        public Material[] GetMaterials()
        {
            if (_materials == null)
            {
                _materials = this.Renderer.sharedMaterials;
            }
            return _materials;
        }

        #endregion

    }
}