﻿using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Kawashirov.FLT.Commons;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.FLT  
{
	[CreateAssetMenu(menuName = "Kawashirov Flat Lit Toon Shader Generator")]
	[Serializable]
	public class Generator : ScriptableObject {
		private static readonly string NAME_PATTERN = @"[a-zA-Z0-9_-]+";

		public string shaderName = "";
		public ShaderComplexity complexity = ShaderComplexity.VF;
		public TessPartitioning tessPartitioning = TessPartitioning.Integer;
		public TessDomain tessDomain = TessDomain.Triangles;
		public BlendTemplate mode = BlendTemplate.Opaque;
		public CullMode cull = CullMode.Back;
		public bool instancing = true;
		public int queueOffset = 0;
		public bool disableBatching = false;
		public bool forceNoShadowCasting = false;
		public bool ignoreProjector = true;
		//public bool zWrite = true;
		public bool debug = false;

		public MainTexKeywords mainTex = MainTexKeywords.ColorMask;
		public CutoutMode cutout = CutoutMode.Classic;
		public bool emission = true;
		public EmissionMode emissionMode = EmissionMode.Custom;
		public bool bumpMap = false;

		public bool rndMixTime = false;
		public bool rndMixCords = false;
		[NonSerialized] private bool needRandomVert = false;
		[NonSerialized] private bool needRandomFrag = false;

		public ShadingMode shading = ShadingMode.KawashirovFLTSingle;

		public bool distanceFade = false;
		public DistanceFadeMode distanceFadeMode = DistanceFadeMode.Range;

		public bool FPS = false;
		public FPSMode FPSMode = FPSMode.ColorTint;

		public bool outline = false;
		public OutlineMode outlineMode = OutlineMode.Tinted;

		public bool infinityWarDecimation = false;

		public bool pcw = false;
		public PolyColorWaveMode pcwMode = PolyColorWaveMode.Classic;

		public Shader result = null;


		public void Generate()
		{
			var assets = AssetDatabase.FindAssets("KawaFLT_Struct_Shared");
			if (assets.Length != 1) {
				EditorUtility.DisplayDialog(
					"Asset not found",
					"Can not find KawaFLT_Struct_Shared.cginc: \n" + string.Join(",\n", assets),
					"OK"
				);
				return;
			}
			var shader_template_guid = assets[0];
			var shader_template_path = AssetDatabase.GUIDToAssetPath(shader_template_guid);
			//EditorUtility.DisplayDialog("shader_template_path", shader_template_path, "OK");
			var shader_template = AssetDatabase.LoadAssetAtPath(shader_template_path, typeof(TextAsset)) as TextAsset;
			// EditorUtility.DisplayDialog("TextAsset", shader_template.text, "OK");
			var shader_cginc_path = System.IO.Path.GetDirectoryName(shader_template_path);
			//EditorUtility.DisplayDialog("shader_cginc_path", shader_cginc_path, "OK");

			var shader_generated_path = string.Format("{0}/_generated_{1}.shader", shader_cginc_path, this.shaderName);

			if (this.result == null) {
				this.result = AssetDatabase.LoadAssetAtPath<Shader>(shader_generated_path);
				if (this.result != null) {
					Debug.LogWarningFormat(
						"Bound shader asset was null, picking up existing asset: {0} at {1}",
						this.result, shader_generated_path
					);
				}
			}

			if (this.result != null) {
				var shader_asset = AssetDatabase.LoadAssetAtPath<Shader>(shader_generated_path);
				if (this.result != shader_asset) {
					// Шейдер, указанный в result не совпадает с необходимым путем
					if (shader_asset != null) {
						// Удаляем другой шейдер, который лежит по целевому пути
						Debug.LogWarningFormat("Removing old asset at target path: \"{0}\"...", shader_generated_path);
						AssetDatabase.DeleteAsset(shader_generated_path);
					}
					var old_path = AssetDatabase.GetAssetPath(this.result);
					Debug.LogWarningFormat(
						"Moving bound shader asset {0} from \"{1}\" to \"{2}\"...",
						this.result, old_path, shader_generated_path
					);
					AssetDatabase.MoveAsset(old_path, shader_generated_path);
				}
			}

			var shader = new ShaderSetup {
				name = this.shaderName.Trim()
			};

			var self_path = AssetDatabase.GetAssetPath(this);
			if (string.IsNullOrEmpty(self_path)) {
				Debug.LogWarningFormat("Generator {0} is not saved to asset file, GUID will not be writen into shader.", this);
			} else {
				var self_guid = AssetDatabase.AssetPathToGUID(self_path);
				if (string.IsNullOrEmpty(self_guid)) {
					Debug.LogWarningFormat("Generator {0} does not have GUID, so it will not be writen into shader.", this);
				} else {
					shader.tags[KawaFLT_GenaratorGUID] = self_guid;
				}
			}

			if (string.IsNullOrEmpty(shader.name)) {
				EditorUtility.DisplayDialog("Invalid Shader Name", "Shader Name is Empty!", "OK");
				return;
			}
			if (!Regex.Match(shader.name, NAME_PATTERN).Success) {
				EditorUtility.DisplayDialog("Invalid Shader Name", "Shader Name should match " + NAME_PATTERN, "OK");
				return;
			}

			this.ConfigureGeneral(ref shader);
			this.ConfigureTess(ref shader);
			this.ConfigureBlending(ref shader);
			this.ConfigureFeatureMainTex(ref shader);
			this.ConfigureFeatureCutoff(ref shader);
			this.ConfigureFeatureEmission(ref shader);
			this.ConfigureFeatureNormalMap(ref shader);

			this.ConfigureFeatureShading(ref shader);

			this.ConfigureFeatureDistanceFade(ref shader);
			this.ConfigureFeatureFPS(ref shader);

			this.ConfigureFeatureOutline(ref shader);
			this.ConfigureFeatureInfinityWarDecimation(ref shader);
			this.ConfigureFeaturePolyColorWave(ref shader);

			this.ConfigureFeaturePRNG(ref shader);

			var code = new StringBuilder(1024 * 12);
			code.Append("// DO NOT EDIT THIS FILE\n");
			code.Append("// It's genarated by scripts and used by scripts.\n");
			code.Append("// НЕ ИЗМЕНЯЙТЕ ЭТОТ ФАЙЛ\n");
			code.Append("// Он сгенерирован скриптами и используется скриптами.\n");
			shader.Bake(ref code);

			using (var writer = new StreamWriter(shader_generated_path)) {
				writer.Write(code.ToString());
				writer.Flush();
			}
			AssetDatabase.ImportAsset(shader_generated_path);
			this.result = AssetDatabase.LoadAssetAtPath<Shader>(shader_generated_path);
			AssetDatabase.SetLabels(this.result, new string[] { "Kawashirov-Generated-Shader-File", "Kawashirov", "Generated" });
			Shader.WarmupAllShaders();
			//EditorUtility.FocusProjectWindow();
		}

		private void ConfigureGeneral(ref ShaderSetup shader)
		{
			this.needRandomVert = false;
			this.needRandomFrag = false;
			shader.SkipCommonStaticVariants();
			shader.Debug(this.debug);

			var f_batching = !this.disableBatching;
			var f_instancing = this.instancing;
			if (this.complexity == ShaderComplexity.VHDGF) {
				f_instancing = false;
				f_batching = false;
			}
			if (this.infinityWarDecimation) {
				f_instancing = false;
				f_batching = false;
			}
			if (f_batching == false) {
				f_instancing = false;
			}

			shader.TagBool(KawaFLT_Feature_Instancing, f_instancing);
			shader.TagBool(Unity_Feature_DisableBatching, !f_batching);
			shader.TagBool(Unity_Feature_ForceNoShadowCasting, this.forceNoShadowCasting);
			shader.TagBool(Unity_Feature_IgnoreProjector, this.ignoreProjector);

			switch (this.complexity) {
				case ShaderComplexity.VHDGF:
					shader.TagBool(KawaFLT_Feature_Geometry, true);
					shader.TagBool(KawaFLT_Feature_Tessellation, true);
					shader.Include("KawaFLT_Struct_VHDGF.cginc");
					shader.Include("KawaFLT_PreFrag_VHDGF.cginc");
					shader.Define("KAWAFLT_PIPELINE_VHDGF 1");
					shader.Define("KAWAFLT_F_GEOMETRY 1");
					shader.Define("KAWAFLT_F_TESSELLATION 1");
					break;
				case ShaderComplexity.VGF:
					shader.TagBool(KawaFLT_Feature_Geometry, true);
					shader.TagBool(KawaFLT_Feature_Tessellation, false);
					shader.Include("KawaFLT_Struct_VGF.cginc");
					shader.Include("KawaFLT_PreFrag_VGF.cginc");
					shader.Define("KAWAFLT_PIPELINE_VGF 1");
					shader.Define("KAWAFLT_F_GEOMETRY 1");
					break;
				default:
					shader.TagBool(KawaFLT_Feature_Geometry, false);
					shader.TagBool(KawaFLT_Feature_Tessellation, false);
					shader.Include("KawaFLT_Struct_VF.cginc");
					shader.Include("KawaFLT_PreFrag_VF.cginc");
					shader.Define("KAWAFLT_PIPELINE_VF 1");
					break;
			}

			//shader.forward.zWrite = this.zWrite;
			shader.forward.cullMode = this.cull;
			shader.forward.multi_compile_instancing = f_instancing;
			shader.forward.defines.Add("KAWAFLT_PASS_FORWARDBASE 1");
			shader.forward.includes.Add("KawaFLT_Frag_ForwardBase.cginc");
			shader.forward.vertex = "vert";
			shader.forward.hull = this.complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.forward.domain = this.complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.forward.geometry = this.complexity == ShaderComplexity.VHDGF || this.complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.forward.fragment = "frag_forwardbase";

			shader.forward_add.zWrite = false;
			shader.forward_add.cullMode = this.cull;
			shader.forward_add.multi_compile_instancing = f_instancing;
			shader.forward_add.defines.Add("KAWAFLT_PASS_FORWARDADD 1");
			shader.forward_add.includes.Add("KawaFLT_Frag_ForwardAdd.cginc");
			shader.forward_add.vertex = "vert";
			shader.forward_add.hull = this.complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.forward_add.domain = this.complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.forward_add.geometry = this.complexity == ShaderComplexity.VHDGF || this.complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.forward_add.fragment = "frag_forwardadd";

			shader.shadowcaster.cullMode = this.cull;
			shader.shadowcaster.multi_compile_instancing = f_instancing;
			shader.shadowcaster.defines.Add("KAWAFLT_PASS_SHADOWCASTER 1");
			shader.shadowcaster.includes.Add("KawaFLT_Frag_ShadowCaster.cginc");
			shader.shadowcaster.vertex = "vert";
			shader.shadowcaster.hull = this.complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.shadowcaster.domain = this.complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.shadowcaster.geometry = this.complexity == ShaderComplexity.VHDGF || this.complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.shadowcaster.fragment = "frag_shadowcaster";
		}


		private void ConfigureTess(ref ShaderSetup shader)
		{
			if (this.complexity != ShaderComplexity.VHDGF)
				return;

			shader.TagEnum(KawaFLT_Feature_Partitioning, this.tessPartitioning);
			switch (this.tessPartitioning) {
				case TessPartitioning.Integer:
					shader.Define("TESS_P_INT 1");
					break;
				case TessPartitioning.FractionalEven:
					shader.Define("TESS_P_EVEN 1");
					break;
				case TessPartitioning.FractionalOdd:
					shader.Define("TESS_P_ODD 1");
					break;
				case TessPartitioning.Pow2:
					shader.Define("TESS_P_POW2 1");
					break;
			}

			shader.TagEnum(KawaFLT_Feature_Domain, this.tessDomain);
			switch (this.tessDomain) {
				case TessDomain.Triangles:
					shader.Define("TESS_D_TRI 1");
					break;
				case TessDomain.Quads:
					shader.Define("TESS_D_QUAD 1");
					break;
			}

			shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Uni", defualt = 1, range = new Vector2(0.9f, 10), power = 2 });
			shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Nrm", defualt = 1, range = new Vector2(0, 20), power = 2 });
			shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Inside", defualt = 1, range = new Vector2(0.1f, 10), power = 10 });
		}

		private void ConfigureBlending(ref ShaderSetup shader)
		{
			string q = null;
			switch (this.mode) {
				case BlendTemplate.Opaque:
					q = "Geometry";
					shader.tags[Unity_RenderType] = "Opaque";
					shader.tags[KawaFLT_RenderType] = "Opaque";
					shader.forward.srcBlend = BlendMode.One;
					shader.forward.dstBlend = BlendMode.Zero;
					shader.forward.zWrite = true;
					shader.forward_add.srcBlend = BlendMode.One;
					shader.forward_add.dstBlend = BlendMode.One;
					shader.forward_add.zWrite = false;
					break;
				case BlendTemplate.Cutout:
					q = "AlphaTest";
					shader.tags[Unity_RenderType] = "TransparentCutout";
					shader.tags[KawaFLT_RenderType] = "Cutout";
					shader.Define("_ALPHATEST_ON 1");
					shader.forward.srcBlend = BlendMode.One;
					shader.forward.dstBlend = BlendMode.Zero;
					shader.forward.zWrite = true;
					shader.forward_add.srcBlend = BlendMode.One;
					shader.forward_add.dstBlend = BlendMode.One;
					shader.forward_add.zWrite = false;
					break;
				case BlendTemplate.Fade:
					q = "Transparent";
					shader.tags[Unity_RenderType] = "Transparent";
					shader.tags[KawaFLT_RenderType] = "Fade";
					shader.Define("_ALPHABLEND_ON 1");
					shader.forward.srcBlend = BlendMode.SrcAlpha;
					shader.forward.dstBlend = BlendMode.OneMinusSrcAlpha;
					shader.forward.zWrite = false;
					shader.forward_add.srcBlend = BlendMode.SrcAlpha;
					shader.forward_add.dstBlend = BlendMode.One;
					shader.forward_add.zWrite = false;
					break;
			}
			shader.tags["Queue"] = string.Format("{0}{1:+#;-#;+0}", q, this.queueOffset);
			shader.shadowcaster.srcBlend = null;
			shader.shadowcaster.dstBlend = null;
			shader.shadowcaster.zWrite = null;
		}

		private void ConfigureFeatureMainTex(ref ShaderSetup shader)
		{
			var mainTex = this.mainTex;
			if (this.mode == BlendTemplate.Cutout && mainTex == MainTexKeywords.NoMainTex)
				mainTex = MainTexKeywords.NoMask;

			shader.TagEnum(KawaFLT_Feature_MainTex, mainTex);

			switch (this.mainTex) {
				case MainTexKeywords.NoMainTex:
					shader.Define("MAINTEX_OFF 1");
					break;
				case MainTexKeywords.NoMask:
					shader.Define("MAINTEX_NOMASK 1");
					break;
				case MainTexKeywords.ColorMask:
					shader.Define("MAINTEX_COLORMASK 1");
					break;
			}

			if (this.mainTex == MainTexKeywords.ColorMask || this.mainTex == MainTexKeywords.NoMask) {
				shader.properties.Add(new Property2D() { name = "_MainTex" });
				if (this.mainTex == MainTexKeywords.ColorMask) {
					shader.properties.Add(new Property2D() { name = "_ColorMask", defualt = "black" });
				}
			}

			shader.properties.Add(new PropertyColor() { name = "_Color" });
		}

		private void ConfigureFeatureCutoff(ref ShaderSetup shader)
		{
			if (this.mode == BlendTemplate.Cutout || (this.mode == BlendTemplate.Fade && this.forceNoShadowCasting == false)) {
				shader.Define("CUTOFF_ON 1");
				shader.TagEnum(KawaFLT_Feature_Cutout, this.cutout);
				switch (this.cutout) {
					case CutoutMode.Classic:
						shader.Define("CUTOFF_CLASSIC 1");
						break;
					case CutoutMode.RangeRandom:
						shader.Define("CUTOFF_RANDOM 1");
						this.needRandomFrag = true;
						break;
				}
				if (this.cutout == CutoutMode.Classic) {
					shader.properties.Add(new PropertyFloat() { name = "_Cutoff", defualt = 0.5f, range = new Vector2(0, 1) });
				}
				if (this.cutout == CutoutMode.RangeRandom || this.cutout == CutoutMode.RangePattern) {
					shader.properties.Add(new PropertyFloat() { name = "_CutoffMin", defualt = 0.4f, range = new Vector2(0, 1) });
					shader.properties.Add(new PropertyFloat() { name = "_CutoffMax", defualt = 0.6f, range = new Vector2(0, 1) });
				}
				if (this.cutout == CutoutMode.RangePattern) {
					shader.properties.Add(new Property2D() { name = "_CutoffPattern", defualt = "gray" });
				}
			} else {
				shader.Define("CUTOFF_OFF 1");
			}
		}

		private void ConfigureFeatureEmission(ref ShaderSetup shader)
		{
			shader.TagBool(KawaFLT_Feature_Emission, this.emission);
			if (this.emission) {
				shader.forward.defines.Add("_EMISSION");
				shader.TagEnum(KawaFLT_Feature_EmissionMode, this.emissionMode);
				switch (this.emissionMode) {
					case EmissionMode.AlbedoNoMask:
						shader.forward.defines.Add("EMISSION_ALBEDO_NOMASK 1");
						break;
					case EmissionMode.AlbedoMask:
						shader.forward.defines.Add("EMISSION_ALBEDO_MASK 1");
						break;
					case EmissionMode.Custom:
						shader.forward.defines.Add("EMISSION_CUSTOM 1");
						break;
				}
				if (this.emissionMode == EmissionMode.AlbedoMask || this.emissionMode == EmissionMode.Custom) {
					shader.properties.Add(new Property2D() { name = "_EmissionMap", defualt = "black" });
				}
				shader.properties.Add(new PropertyColor() { name = "_EmissionColor", defualt = Color.black });
			} else {
				shader.forward.defines.Add("EMISSION_OFF 1");
			}
		}

		private void ConfigureFeatureNormalMap(ref ShaderSetup shader)
		{
			shader.TagBool(KawaFLT_Feature_NormalMap, this.bumpMap);
			if (this.bumpMap) {
				shader.forward.defines.Add("_NORMALMAP");
				shader.forward_add.defines.Add("_NORMALMAP");
				shader.properties.Add(new Property2D() { name = "_BumpMap", defualt = "bump", isNormal = true });
				shader.properties.Add(new PropertyFloat() { name = "_BumpScale", defualt = 1.0f });
			}
		}

		private void ConfigureFeatureShading(ref ShaderSetup shader)
		{
			shader.TagEnum(KawaFLT_Feature_Shading, this.shading);
			switch (this.shading) {
				case ShadingMode.CubedParadoxFLT:
					shader.forward.defines.Add("SHADE_CUBEDPARADOXFLT 1");
					shader.forward_add.defines.Add("SHADE_CUBEDPARADOXFLT 1");
					this.ConfigureFeatureShadingCubedParadox(ref shader);
					break;
				case ShadingMode.KawashirovFLTSingle:
					shader.forward.defines.Add("SHADE_KAWAFLT_SINGLE 1");
					shader.forward_add.defines.Add("SHADE_KAWAFLT_SINGLE 1");
					this.ConfigureFeatureShadingKawashirovFLTSingle(ref shader);
					break;
				case ShadingMode.KawashirovFLTRamp:
					shader.forward.defines.Add("SHADE_KAWAFLT_RAMP 1");
					shader.forward_add.defines.Add("SHADE_KAWAFLT_RAMP 1");
					this.ConfigureFeatureShadingKawashirovFLTRamp(ref shader);
					break;
			}
		}

		private void ConfigureFeatureShadingCubedParadox(ref ShaderSetup shader) {
			shader.properties.Add(new PropertyFloat() { name = "_Sh_Cbdprdx_Shadow", defualt = 0.4f, range = new Vector2(0, 1) });
		}

		private void ConfigureFeatureShadingKawashirovFLTSingle(ref ShaderSetup shader)
		{
			shader.properties.Add(new PropertyFloat() { name = "_Sh_Kwshrv_ShdBlnd", defualt = 0.7f, range = new Vector2(0, 1), power = 2 });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_TngntLo", defualt = 0.7f, range = new Vector2(0, 1), power = 1.5f });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_TngntHi", defualt = 0.8f, range = new Vector2(0, 1), power = 1.5f });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_ShdLo", defualt = 0.4f, range = new Vector2(0, 1) });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_ShdHi", defualt = 0.9f, range = new Vector2(0, 1) });
		}

		private void ConfigureFeatureShadingKawashirovFLTRamp(ref ShaderSetup shader)
		{
			shader.properties.Add(new PropertyFloat() { name = "_Sh_Kwshrv_ShdBlnd", defualt = 0.7f, range = new Vector2(0, 1), power = 2 });
			shader.properties.Add(new Property2D() { name = "_Sh_KwshrvRmp_Tex", defualt = "gray" });
			shader.properties.Add(new PropertyColor() { name = "_Sh_KwshrvRmp_NdrctClr", defualt = Color.white });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_TngntLo", defualt = 0.7f, range = new Vector2(0, 1), power = 1.5f });
		}

		private void ConfigureFeatureDistanceFade(ref ShaderSetup shader)
		{
			shader.TagBool(KawaFLT_Feature_DistanceFade, this.distanceFade);
			if (this.distanceFade) {
				shader.Define("DSTFD_ON 1");
				this.needRandomFrag = true;
				shader.TagEnum(KawaFLT_Feature_DistanceFadeMode, this.distanceFadeMode);
				switch (this.distanceFadeMode) {
					case DistanceFadeMode.Range:
						shader.Define("DSTFD_RANGE 1");
						break;
					case DistanceFadeMode.Infinity:
						shader.Define("DSTFD_INFINITY 1");
						break;
				}
				shader.properties.Add(new PropertyVector() { name = "_DstFd_Axis", defualt = Vector4.one });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_Near", defualt = 1.0f });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_Far", defualt = 2.0f });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_AdjustPower", defualt = 1.0f, range = new Vector2(0.1f, 10), power = 10 });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_AdjustScale", defualt = 1.0f, range = new Vector2(0.1f, 10) });
			} else {
				shader.Define("DSTFD_OFF 1");
			}
		}

		private void ConfigureFeatureFPS(ref ShaderSetup shader)
		{
			shader.TagBool(KawaFLT_Feature_FPS, this.FPS);
			if (this.FPS) {
				shader.TagEnum(KawaFLT_Feature_FPSMode, this.FPSMode);
				switch (this.FPSMode) {
					case FPSMode.ColorTint:
						shader.Define("FPS_COLOR 1");
						break;
					case FPSMode.DigitsTexture:
						shader.Define("FPS_TEX 1");
						break;
					case FPSMode.DigitsMesh:
						shader.Define("FPS_MESH 1");
						break;
				}
				shader.properties.Add(new PropertyColor() { name = "_FPS_TLo", defualt = new Color(1, 0.5f, 0.5f, 1) });
				shader.properties.Add(new PropertyColor() { name = "_FPS_THi", defualt = new Color(0.5f, 1, 0.5f, 1) });
			} else {
				shader.Define("FPS_OFF 1");
			}
		}

		private void ConfigureFeatureOutline(ref ShaderSetup shader)
		{
			shader.TagBool(KawaFLT_Feature_Outline, this.outline);
			if (this.outline) {
				shader.Define("OUTLINE_ON 1");
				shader.TagEnum(KawaFLT_Feature_OutlineMode, this.outlineMode);
				if (this.outlineMode == OutlineMode.Colored) {
					shader.Define("OUTLINE_COLORED 1");
				} else if (this.outlineMode == OutlineMode.Tinted) {
					shader.Define("OUTLINE_TINTED 1");
				}
				shader.properties.Add(new PropertyFloat() { name = "_outline_width", defualt = 0.2f, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyColor() { name = "_outline_color", defualt = new Color(0.5f, 0.5f, 0.5f, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_outline_bias", defualt = 0, range = new Vector2(-1, 5) });
			} else {
				shader.Define("OUTLINE_OFF 1");
			}
		}

		private void ConfigureFeatureInfinityWarDecimation(ref ShaderSetup shader)
		{
			shader.TagBool(KawaFLT_Feature_InfinityWarDecimation, this.infinityWarDecimation);
			if (this.infinityWarDecimation) {
				this.needRandomVert = true;
				shader.Define("DSNTGRT_ON 1");
				shader.properties.Add(new PropertyVector() { name = "_Dsntgrt_Plane", defualt = Vector4.zero });
				shader.properties.Add(new PropertyFloat() { name = "_Dsntgrt_TriSpreadAccel", defualt = 2, range = new Vector2(0, 5) });
				shader.properties.Add(new PropertyFloat() { name = "_Dsntgrt_TriSpreadFactor", defualt = 0.1f, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_Dsntgrt_TriDecayNear", defualt = 0, range = new Vector2(0, 10) });
				shader.properties.Add(new PropertyFloat() { name = "_Dsntgrt_TriDecayFar", defualt = 0.5f, range = new Vector2(0, 10) });
				shader.properties.Add(new PropertyFloat() { name = "_Dsntgrt_TriPowerAdjust", defualt = 1, range = new Vector2(0.5f, 2) });
				shader.properties.Add(new PropertyColor() { name = "_Dsntgrt_Tint", defualt = new Color(0.2f, 0.2f, 0.2f, 0.1f) });
				if (this.complexity == ShaderComplexity.VHDGF) {
					shader.properties.Add(new PropertyFloat() { name = "_Dsntgrt_Tsltn", defualt = 1, range = new Vector2(0, 10), power = 2 });
				}
			} else {
				shader.Define("DSNTGRT_OFF 1");
			}
		}

		private void ConfigureFeaturePolyColorWave(ref ShaderSetup shader)
		{
			shader.TagBool(KawaFLT_Feature_PCW, this.pcw);
			if (this.pcw) {
				this.needRandomVert = true;
				shader.Define("PCW_ON 1");
				shader.TagEnum(KawaFLT_Feature_PCWMode, this.pcwMode);
				if (this.pcw) {
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmLo", defualt = 4 });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmAs", defualt = 0.25f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmHi", defualt = 0.5f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmDe", defualt = 0.25f });
					shader.properties.Add(new PropertyVector() { name = "_PCW_WvTmUV", defualt = new Vector4(0, 10, 0, 0) });
					shader.properties.Add(new PropertyVector() { name = "_PCW_WvTmVtx", defualt = new Vector4(0, 10, 0, 0) });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmRnd", defualt = 5 });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_Em", defualt = 0.5f, range = new Vector2(0, 1), power = 2 });
					shader.properties.Add(new PropertyColor() { name = "_PCW_Color", defualt = Color.white });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwTm", defualt = 0.5f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwTmRnd", defualt = 0.5f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwStrtn", defualt = 1, range = new Vector2(0, 1) });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwBrghtnss", defualt = 0.5f, range = new Vector2(0, 1) });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_Mix", defualt = 0.5f, range = new Vector2(0, 1) });
				}
			} else {
				shader.Define("PCW_OFF 1");
			}
		}

		private void ConfigureFeaturePRNG(ref ShaderSetup shader)
		{
			if (this.needRandomVert) {
				shader.Define("RANDOM_VERT 1");
			}
			if (this.needRandomFrag) {
				shader.Define("RANDOM_FRAG 1");
			}
			if (this.needRandomVert || this.needRandomFrag) {
				shader.TagBool(KawaFLT_Feature_Random, true);
				shader.Define("RANDOM_SEED_TEX 1");
				shader.properties.Add(new Property2D() { name = "_Rnd_Seed", defualt = "gray" });
				if (this.rndMixTime) {
					shader.Define("RANDOM_MIX_TIME 1");
				}
				if (this.rndMixCords) {
					shader.Define("RANDOM_MIX_COORD 1");
				}
			} else {
				shader.TagBool(KawaFLT_Feature_Random, false);
			}
		}

	}
}