#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using UniGLTF.Utils;

namespace UniHumanoid
{
	// TODO: Split into BoneLimit.cs (after v0.104)
	[Serializable]
	public struct BoneLimit
	{
		public HumanBodyBones humanBone;
		public string boneName;
		public bool useDefaultValues;
		public Vector3 min;
		public Vector3 max;
		public Vector3 center;
		public float axisLength;


		// HumanTrait.BoneName corresponds almost one-to-one with HumanBodyBones.ToString, but
		// there is a subtle difference in the presence or absence of spaces for finger bones.
		// This space is required by AvatarBuilder.BuildHumanAvatar,
		// and it is necessary to distinguish it from HumanBodyBones.ToString.
		//
		// Also, cache the following to avoid GC allocations.
		// * HumanTrait.BoneName
		// * traitName.Replace
		// * Enum.Parse
		private static readonly Dictionary<HumanBodyBones, string> cachedHumanBodyBonesToBoneTraitNameMap =
			HumanTrait.BoneName.ToDictionary(
				traitName => (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), traitName.Replace(" ", string.Empty)),
				traitName => traitName);

		// Reverse mapping
		private static readonly Dictionary<string, HumanBodyBones> cachedBoneTraitNameToHumanBodyBonesMap =
			HumanTrait.BoneName.ToDictionary(
				traitName => traitName,
				traitName => (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), traitName.Replace(" ", string.Empty)));

		public static BoneLimit From(HumanBone bone)
		{
			Debug.Log($"Creating BoneLimit from HumanBone:" +
				$"\n  Bone Name: {bone.boneName}" +
				$"\n  Human Name: {bone.humanName}" +
				$"\n  Mapped Enum: {(cachedBoneTraitNameToHumanBodyBonesMap.TryGetValue(bone.humanName, out HumanBodyBones mapped) ? mapped.ToString() : "NOT FOUND")}");

			return new BoneLimit
			{
				humanBone = cachedBoneTraitNameToHumanBodyBonesMap[bone.humanName],
				boneName = bone.boneName,
				useDefaultValues = bone.limit.useDefaultValues,
				min = bone.limit.min,
				max = bone.limit.max,
				center = bone.limit.center,
				axisLength = bone.limit.axisLength,
			};
		}

		public HumanBone ToHumanBone()
		{
			Debug.Log($"Converting BoneLimit to HumanBone:" +
				$"\n  Bone Name: {boneName}" +
				$"\n  Human Bone Enum: {humanBone}" +
				$"\n  Mapped Trait Name: {(cachedHumanBodyBonesToBoneTraitNameMap.TryGetValue(humanBone, out string traitName) ? traitName : "NOT FOUND")}");

			var newHumanBone = new HumanBone
			{
				boneName = boneName,
				humanName = cachedHumanBodyBonesToBoneTraitNameMap[this.humanBone],
				limit = new HumanLimit
				{
					useDefaultValues = useDefaultValues,
					axisLength = axisLength,
					center = center,
					max = max,
					min = min
				},
			};

			Debug.Log($"Created HumanBone:" +
				$"\n  Bone Name: {newHumanBone.boneName}" +
				$"\n  Human Name: {newHumanBone.humanName}");

			return newHumanBone;
		}
	}


	[Serializable]
	public class AvatarDescription : ScriptableObject
	{
		public float armStretch = 0.05f;
		public float legStretch = 0.05f;
		public float upperArmTwist = 0.5f;
		public float lowerArmTwist = 0.5f;
		public float upperLegTwist = 0.5f;
		public float lowerLegTwist = 0.5f;
		public float feetSpacing = 0;
		public bool hasTranslationDoF;
		public BoneLimit[] human;

		public HumanDescription ToHumanDescription(Transform root)
		{
			Debug.Log($"Creating HumanDescription for root: {root.name}");

			// Get all transforms
			var transforms = root.GetComponentsInChildren<Transform>();
			Debug.Log($"Found {transforms.Length} transforms in hierarchy");

			// Create skeleton bones
			var skeletonBones = new SkeletonBone[transforms.Length];
			var index = 0;
			foreach (var t in transforms)
			{
				skeletonBones[index] = t.ToSkeletonBone();
				// Debug.Log($"Skeleton Bone [{index}]: {t.name}" +
				// 	$"\n  Position: {skeletonBones[index].position}" +
				// 	$"\n  Rotation: {skeletonBones[index].rotation}" +
				// 	$"\n  Scale: {skeletonBones[index].scale}");
				index++;
			}

			// Create human bones
			Debug.Log($"Creating {human.Length} human bones");
			var humanBones = new HumanBone[human.Length];
			index = 0;
			foreach (var bonelimit in human)
			{
				humanBones[index] = bonelimit.ToHumanBone();
				// Debug.Log($"Human Bone [{index}]: {humanBones[index].boneName}" +
				// 	$"\n  Human Name: {humanBones[index].humanName}" +
				// 	$"\n  Limit UseDefault: {humanBones[index].limit.useDefaultValues}" +
				// 	$"\n  Limit Min: {humanBones[index].limit.min}" +
				// 	$"\n  Limit Max: {humanBones[index].limit.max}" +
				// 	$"\n  Center: {humanBones[index].limit.center}" +
				// 	$"\n  Axis Length: {humanBones[index].limit.axisLength}");
				index++;
			}

			var description = new HumanDescription
			{
				skeleton = skeletonBones,
				human = humanBones,
				armStretch = armStretch,
				legStretch = legStretch,
				upperArmTwist = upperArmTwist,
				lowerArmTwist = lowerArmTwist,
				upperLegTwist = upperLegTwist,
				lowerLegTwist = lowerLegTwist,
				feetSpacing = feetSpacing,
				hasTranslationDoF = hasTranslationDoF,
			};

			Debug.Log($"HumanDescription created with parameters:" +
				$"\n  Skeleton Bones: {description.skeleton.Length}" +
				$"\n  Human Bones: {description.human.Length}" +
				$"\n  Arm Stretch: {description.armStretch}" +
				$"\n  Leg Stretch: {description.legStretch}" +
				$"\n  Upper Arm Twist: {description.upperArmTwist}" +
				$"\n  Lower Arm Twist: {description.lowerArmTwist}" +
				$"\n  Upper Leg Twist: {description.upperLegTwist}" +
				$"\n  Lower Leg Twist: {description.lowerLegTwist}" +
				$"\n  Feet Spacing: {description.feetSpacing}" +
				$"\n  Has Translation DoF: {description.hasTranslationDoF}");

			// Verify bone mappings
			Debug.Log("Verifying bone mappings:");
			foreach (var humanBone in description.human)
			{
				var skeletonBone = description.skeleton.FirstOrDefault(sb => sb.name == humanBone.boneName);
				if (skeletonBone.name != null)
				{
					Debug.Log($"✓ Found mapping: {humanBone.humanName} -> {humanBone.boneName}");
				}
				else
				{
					Debug.LogWarning($"⚠ Missing skeleton bone for human bone: {humanBone.humanName} -> {humanBone.boneName}");
				}
			}

			return description;
		}

		public Avatar CreateAvatar(Transform root)
		{
			// force unique name
			ForceUniqueName.Process(root);
			return AvatarBuilder.BuildHumanAvatar(root.gameObject, ToHumanDescription(root));
		}

		public Avatar CreateAvatarAndSetup(Transform root)
		{
			var avatar = CreateAvatar(root);
			avatar.name = name;

			if (root.TryGetComponent<Animator>(out var animator))
			{
				var positionMap = root.Traverse().ToDictionary(x => x, x => x.position);
				animator.avatar = avatar;
				foreach (var x in root.Traverse())
				{
					x.position = positionMap[x];
				}
			}

			if (root.TryGetComponent<HumanPoseTransfer>(out var transfer))
			{
				transfer.Avatar = avatar;
			}

			return avatar;
		}

#if UNITY_EDITOR
		public static AvatarDescription CreateFrom(Avatar avatar)
		{
			var description = default(HumanDescription);
			if (!GetHumanDescription(avatar, ref description))
			{
				return null;
			}

			return CreateFrom(description);
		}
#endif

		public static AvatarDescription CreateFrom(HumanDescription description)
		{
			var avatarDescription = ScriptableObject.CreateInstance<AvatarDescription>();
			avatarDescription.name = "AvatarDescription";
			avatarDescription.armStretch = description.armStretch;
			avatarDescription.legStretch = description.legStretch;
			avatarDescription.feetSpacing = description.feetSpacing;
			avatarDescription.hasTranslationDoF = description.hasTranslationDoF;
			avatarDescription.lowerArmTwist = description.lowerArmTwist;
			avatarDescription.lowerLegTwist = description.lowerLegTwist;
			avatarDescription.upperArmTwist = description.upperArmTwist;
			avatarDescription.upperLegTwist = description.upperLegTwist;
			avatarDescription.human = description.human.Select(BoneLimit.From).ToArray();
			return avatarDescription;
		}

		public static AvatarDescription Create(AvatarDescription src = null)
		{
			var avatarDescription = ScriptableObject.CreateInstance<AvatarDescription>();
			avatarDescription.name = "AvatarDescription";
			if (src != null)
			{
				avatarDescription.armStretch = src.armStretch;
				avatarDescription.legStretch = src.legStretch;
				avatarDescription.feetSpacing = src.feetSpacing;
				avatarDescription.upperArmTwist = src.upperArmTwist;
				avatarDescription.lowerArmTwist = src.lowerArmTwist;
				avatarDescription.upperLegTwist = src.upperLegTwist;
				avatarDescription.lowerLegTwist = src.lowerLegTwist;
			}
			else
			{
				avatarDescription.armStretch = 0.05f;
				avatarDescription.legStretch = 0.05f;
				avatarDescription.feetSpacing = 0.0f;
				avatarDescription.lowerArmTwist = 0.5f;
				avatarDescription.upperArmTwist = 0.5f;
				avatarDescription.upperLegTwist = 0.5f;
				avatarDescription.lowerLegTwist = 0.5f;
			}

			return avatarDescription;
		}

		/// <summary>
		/// Create an AvatarDescription from a skeleton and bone transforms.
		/// </summary>
		/// <param name="boneTransforms">The transforms of the bones in the skeleton.</param>
		/// <param name="skeleton">The skeleton to use for the AvatarDescription.</param>
		/// <returns>Returns a new AvatarDescription instance.</returns>
		public static AvatarDescription Create(Transform[] boneTransforms, Skeleton skeleton)
		{
			Debug.Log($"Creating AvatarDescription from {boneTransforms.Length} transforms and skeleton");

			var mappings = skeleton.Bones.Select(x =>
			{
				var bone = new KeyValuePair<HumanBodyBones, Transform>(x.Key, boneTransforms[x.Value]);
				Debug.Log($"Created mapping: {bone.Key} -> {bone.Value.name}");
				return bone;
			});

			return Create(mappings);
		}

		public static AvatarDescription Create(IEnumerable<KeyValuePair<HumanBodyBones, Transform>> skeleton)
		{
			var description = Create();

			Debug.Log("Creating AvatarDescription from skeleton mappings:");
			// foreach (var kvp in skeleton)
			// {
			// 	Debug.Log($"Processing mapping:" +
			// 		$"\n  HumanBodyBone: {kvp.Key}" +
			// 		$"\n  Transform: {kvp.Value.name}");
			// }

			description.SetHumanBones(skeleton);
			return description;
		}

		public void SetHumanBones(IEnumerable<KeyValuePair<HumanBodyBones, Transform>> skeleton)
		{
			Debug.Log("Starting SetHumanBones...");

			human = skeleton.Select(x =>
			{
				Debug.Log($"Creating BoneLimit for Transform: {x.Value?.name}" +
					$"\n  Assigned HumanBodyBones: {x.Key}" +
					$"\n  Expected mapping: {x.Key} should map to {x.Value?.name}");

				var boneLimit = new BoneLimit
				{
					humanBone = x.Key,
					boneName = x.Value.name,
					useDefaultValues = true,
				};

				// Verify the mapping is correct
				if (x.Value.name.Contains("Shoulder") && x.Key != HumanBodyBones.LeftShoulder && x.Key != HumanBodyBones.RightShoulder)
				{
					Debug.LogError($"Incorrect mapping detected! Bone {x.Value.name} is being mapped to {x.Key}");
				}

				return boneLimit;
			}).ToArray();
		}

#if UNITY_EDITOR
		/// <summary>
		/// * https://answers.unity.com/questions/612177/how-can-i-access-human-avatar-bone-and-muscle-valu.html
		/// </summary>
		/// <param name="target"></param>
		/// <param name="des"></param>
		/// <returns></returns>
		public static bool GetHumanDescription(UnityEngine.Object target, ref HumanDescription des)
		{
			if (target != null)
			{
				var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(target));
				if (importer != null)
				{
					Debug.Log("AssetImporter Type: " + importer.GetType());
					ModelImporter modelImporter = importer as ModelImporter;
					if (modelImporter != null)
					{
						des = modelImporter.humanDescription;
						Debug.Log("## Cool stuff data by ModelImporter ##");
						return true;
					}
					else
					{
						Debug.LogWarning("## Please Select Imported Model in Project View not prefab or other things ##");
					}
				}
			}

			return false;
		}
#endif

		public static Avatar CreateAvatarForCopyHierarchy(
			Animator src,
			GameObject dst,
			IDictionary<Transform, Transform> boneMap,
			Action<AvatarDescription> modAvatarDesc = null)
		{
			if (src == null)
			{
				throw new ArgumentNullException("src");
			}

			var srcHumanBones = CachedEnum.GetValues<HumanBodyBones>()
				.Where(x => x != HumanBodyBones.LastBone)
				.Select(x => new { Key = x, Value = src.GetBoneTransform(x) })
				.Where(x => x.Value != null)
				;

			var map =
				   srcHumanBones
				   .Where(x => boneMap.ContainsKey(x.Value))
				   .ToDictionary(x => x.Key, x => boneMap[x.Value])
				   ;

			var avatarDescription = UniHumanoid.AvatarDescription.Create();
			if (modAvatarDesc != null)
			{
				modAvatarDesc(avatarDescription);
			}
			avatarDescription.SetHumanBones(map);
			var avatar = avatarDescription.CreateAvatar(dst.transform);
			avatar.name = "created";
			return avatar;
		}

		public static Avatar RecreateAvatar(Animator src)
		{
			if (src == null)
			{
				throw new ArgumentNullException("src");
			}

			var srcHumanBones = CachedEnum.GetValues<HumanBodyBones>()
				.Where(x => x != HumanBodyBones.LastBone)
				.Select(x => new { Key = x, Value = src.GetBoneTransform(x) })
				.Where(x => x.Value != null)
				;

			var map =
				   srcHumanBones
				   .ToDictionary(x => x.Key, x => x.Value)
				   ;

			var avatarDescription = UniHumanoid.AvatarDescription.Create();
			avatarDescription.SetHumanBones(map);

			var avatar = avatarDescription.CreateAvatar(src.transform);
			avatar.name = "created";
			return avatar;
		}
	}
}
