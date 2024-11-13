using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace UniHumanoid
{
	public interface ISkeletonDetector
	{
		Skeleton Detect(IList<IBone> bones);
	}


	public class BvhSkeletonEstimator : ISkeletonDetector
	{
		static IBone GetRoot(IList<IBone> bones)
		{
			var hips = bones.Where(x => x.Parent == null).ToArray();
			if (hips.Length != 1)
			{
				throw new System.Exception("Require unique root");
			}
			return hips[0];
		}

		static IBone SelectBone(Func<IBone, IBone, IBone> selector, IList<IBone> bones)
		{
			if (bones == null || bones.Count == 0)
				throw new Exception("no bones");
			var current = bones[0];
			for (var i = 1; i < bones.Count; ++i)
			{
				current = selector(current, bones[i]);
			}
			return current;
		}

		static void GetSpineAndHips(IBone hips, out IBone spine, out IBone leg_L, out IBone leg_R)
		{
			if (hips.Children.Count != 3)
				throw new System.Exception("Hips require 3 children");
			spine = SelectBone((l, r) => l.CenterOfDescendant().y > r.CenterOfDescendant().y ? l : r, hips.Children);
			leg_L = SelectBone((l, r) => l.CenterOfDescendant().x < r.CenterOfDescendant().x ? l : r, hips.Children);
			leg_R = SelectBone((l, r) => l.CenterOfDescendant().x > r.CenterOfDescendant().x ? l : r, hips.Children);
		}

		private static void GetNeckAndArms(IBone chest, out IBone neck, out IBone arm_L, out IBone arm_R)
		{
			if (chest.Children.Count != 3)
			{
				Debug.LogError($"Chest requires 3 children, found {chest.Children.Count}");
				throw new System.Exception("Chest require 3 children");
			}

			Debug.Log($"Getting neck and arms from chest: {chest.Name}");
			foreach (var child in chest.Children)
			{
				Debug.Log($"Child bone: {child.Name}");
			}

			// First identify shoulders by name patterns
			var shoulders = chest.Children.Where(b =>
				b.Name.Contains("Shoulder") ||
				b.Name.Contains("Clavicle") ||
				b.Name.Contains("Arm") ||
				b.Name.Contains("shoulder") ||
				b.Name.Contains("clavicle")).ToList();

			// The remaining bone should be the neck
			neck = chest.Children.FirstOrDefault(b => !shoulders.Contains(b));

			if (shoulders.Count != 2 || neck == null)
			{
				// Fallback: try to identify neck by vertical position and name
				var possibleNeck = chest.Children
					.Where(b => b.Name.Contains("Neck") || b.Name.Contains("neck") || b.Name.Contains("Head"))
					.OrderByDescending(b => b.CenterOfDescendant().y)
					.FirstOrDefault();

				if (possibleNeck != null)
				{
					neck = possibleNeck;
					var neckLocal = neck;
					shoulders = chest.Children.Where(b => b != neckLocal).ToList();
				}
				else
				{
					// Last resort: use vertical position only
					var sorted = chest.Children.OrderByDescending(b => b.CenterOfDescendant().y).ToList();
					neck = sorted[0];
					shoulders = sorted.Skip(1).ToList();
				}
			}

			// Sort shoulders by X position
			var orderedShoulders = shoulders.OrderBy(b => b.CenterOfDescendant().x).ToList();
			arm_L = orderedShoulders[0];
			arm_R = orderedShoulders[1];

			Debug.Log($"Final bone identification:" +
				$"\n  Neck: {neck.Name}" +
				$"\n  Left Shoulder: {arm_L.Name}" +
				$"\n  Right Shoulder: {arm_R.Name}");

			// Verify our identification
			if (neck.Name.Contains("Shoulder") || neck.Name.Contains("Hand"))
			{
				Debug.LogError($"Probable misidentification: {neck.Name} identified as neck!");
				throw new Exception("Incorrect neck identification");
			}
		}

		struct Arm
		{
			public IBone Shoulder;
			public IBone UpperArm;
			public IBone LowerArm;
			public IBone Hand;
		}

		Arm GetArm(IBone shoulder)
		{
			var bones = shoulder.Traverse().ToArray();
			switch (bones.Length)
			{
				case 0:
				case 1:
				case 2:
				case 3:
					throw new NotImplementedException();

				default:
					return new Arm
					{
						Shoulder = bones[0],
						UpperArm = bones[1],
						LowerArm = bones[2],
						Hand = bones[3],
					};
			}
		}

		struct Leg
		{
			public IBone UpperLeg;
			public IBone LowerLeg;
			public IBone Foot;
			public IBone Toes;
		}

		Leg GetLeg(IBone leg)
		{
			Debug.Log($"Getting leg bones starting from: {leg.Name}");

			// Get all bones in chain
			var bones = leg.Traverse()
				.Where(x => !x.Name.ToLower().Contains("buttock"))
				.ToArray();

			Debug.Log("Leg bone chain:");
			for (int i = 0; i < bones.Length; i++)
			{
				Debug.Log($"  [{i}] {bones[i].Name}");
			}

			switch (bones.Length)
			{
				case 0:
				case 1:
				case 2:
					Debug.LogError($"Not enough bones in leg chain. Found: {bones.Length}");
					throw new NotImplementedException();

				case 3:
					Debug.Log($"Found basic leg chain:" +
						$"\n  UpperLeg: {bones[0].Name}" +
						$"\n  LowerLeg: {bones[1].Name}" +
						$"\n  Foot: {bones[2].Name}");
					return new Leg
					{
						UpperLeg = bones[0],  // First bone should be UpperLeg
						LowerLeg = bones[1],  // Second bone should be LowerLeg
						Foot = bones[2],      // Third bone should be Foot
					};

				default:
					// Find the correct bones by name pattern
					var upperLeg = bones.FirstOrDefault(b => b.Name.Contains("UpLeg") || b.Name.Contains("Upper"));
					var lowerLeg = bones.FirstOrDefault(b => b.Name.Contains("Leg") && !b.Name.Contains("Up") && !b.Name.Contains("End"));
					var foot = bones.FirstOrDefault(b => b.Name.Contains("Foot"));
					var toes = bones.FirstOrDefault(b => b.Name.Contains("Toe"));

					if (upperLeg == null) upperLeg = bones[0];
					if (lowerLeg == null) lowerLeg = bones[1];
					if (foot == null) foot = bones[2];
					if (toes == null && bones.Length > 3) toes = bones[3];

					Debug.Log($"Found detailed leg chain:" +
						$"\n  UpperLeg: {upperLeg?.Name}" +
						$"\n  LowerLeg: {lowerLeg?.Name}" +
						$"\n  Foot: {foot?.Name}" +
						$"\n  Toes: {toes?.Name}");

					return new Leg
					{
						UpperLeg = upperLeg,
						LowerLeg = lowerLeg,
						Foot = foot,
						Toes = toes
					};
			}
		}

		public Skeleton Detect(IList<IBone> bones)
		{
			Debug.Log("Starting skeleton detection...");
			var root = GetRoot(bones);
			Debug.Log($"Found root bone: {root.Name}");

			// Find hips - first bone with 3 children (typically 2 legs + spine)
			var hips = root.Traverse().First(x => x.Children.Count == 3);
			Debug.Log($"Found hips: {hips.Name} with {hips.Children.Count} children");

			// Get spine and hip bones
			IBone spine, hip_L, hip_R;
			GetSpineAndHips(hips, out spine, out hip_L, out hip_R);
			Debug.Log($"Found spine and hips:\n  Spine: {spine.Name}\n  Left Hip: {hip_L.Name}\n  Right Hip: {hip_R.Name}");

			// Process legs
			var legLeft = GetLeg(hip_L);
			var legRight = GetLeg(hip_R);

			// Build spine chain up to chest
			var spineToChest = new List<IBone>();
			var current = spine;
			while (current != null && spineToChest.Count < 4)  // Limit to avoid over-traversal
			{
				Debug.Log($"Processing potential spine bone: {current.Name}" +
					$"\n  Position: {current.CenterOfDescendant()}" +
					$"\n  Children count: {current.Children.Count}");

				spineToChest.Add(current);

				// Check if we've reached chest level
				if (current.Children.Count == 3)
				{
					var hasShoulderOrNeck = current.Children.Any(c =>
						c.Name.ToLower().Contains("shoulder") ||
						c.Name.ToLower().Contains("neck") ||
						c.Name.ToLower().Contains("clav") ||  // clavicle
						c.Name.ToLower().Contains("arm"));

					if (hasShoulderOrNeck)
					{
						Debug.Log($"Found chest level bone: {current.Name} (verified by shoulder/neck presence)");
						break;
					}
				}

				// Get next spine bone - prefer child with most descendants
				current = current.Children
					.Where(c => !c.Name.ToLower().Contains("shoulder") &&
							!c.Name.ToLower().Contains("neck") &&
							!c.Name.ToLower().Contains("arm"))
					.OrderByDescending(c => c.Traverse().Count())
					.FirstOrDefault();
			}

			// Verify spine chain
			if (spineToChest.Count < 2)
			{
				Debug.LogError("Spine chain too short - expected at least 2 bones");
			}

			foreach (var bone in spineToChest)
			{
				Debug.Log($"Spine chain bone: {bone.Name}" +
					$"\n  Position: {bone.CenterOfDescendant()}" +
					$"\n  Children: {bone.Children.Count}");
			}

			// Get neck and shoulders
			IBone neck, shoulder_L, shoulder_R;
			GetNeckAndArms(spineToChest.Last(), out neck, out shoulder_L, out shoulder_R);
			Debug.Log($"Found neck and shoulders:\n  Neck: {neck.Name}\n  Left Shoulder: {shoulder_L.Name}\n  Right Shoulder: {shoulder_R.Name}");

			// Process arms
			var armLeft = GetArm(shoulder_L);
			var armRight = GetArm(shoulder_R);

			// Get neck to head chain
			var neckToHead = neck.Traverse()
				.TakeWhile(b => !b.Name.ToLower().Contains("shoulder") &&
							!b.Name.ToLower().Contains("arm"))
				.ToArray();

			Debug.Log($"Neck to head chain: {string.Join(" -> ", neckToHead.Select(b => b.Name))}");

			// Create skeleton and start mapping bones
			Debug.Log("Creating skeleton and setting bones...");
			var skeleton = new Skeleton();

			// Map hips
			Debug.Log($"Setting Hips: {hips.Name}");
			skeleton.Set(HumanBodyBones.Hips, bones, hips);

			// Set spine chain
			Debug.Log($"Setting spine chain. Count: {spineToChest.Count}");
			switch (spineToChest.Count)
			{
				case 0:
					throw new Exception();

				case 1:
					skeleton.Set(HumanBodyBones.Spine, bones, spineToChest[0]);
					break;

				case 2:
					skeleton.Set(HumanBodyBones.Spine, bones, spineToChest[0]);
					skeleton.Set(HumanBodyBones.Chest, bones, spineToChest[1]);
					break;

#if UNITY_5_6_OR_NEWER
				case 3:
					skeleton.Set(HumanBodyBones.Spine, bones, spineToChest[0]);
					skeleton.Set(HumanBodyBones.Chest, bones, spineToChest[1]);
					skeleton.Set(HumanBodyBones.UpperChest, bones, spineToChest[2]);
					break;
#endif

				default:
					skeleton.Set(HumanBodyBones.Spine, bones, spineToChest[0]);
#if UNITY_5_6_OR_NEWER
					skeleton.Set(HumanBodyBones.Chest, bones, spineToChest[1]);
					skeleton.Set(HumanBodyBones.UpperChest, bones, spineToChest.Last());
#else
                    skeleton.Set(HumanBodyBones.Chest, bones, spineToChest.Last());
#endif
					break;
			}

			switch (neckToHead.Length)
			{
				case 0:
					throw new Exception();

				case 1:
					skeleton.Set(HumanBodyBones.Head, bones, neckToHead[0]);
					break;

				case 2:
					skeleton.Set(HumanBodyBones.Neck, bones, neckToHead[0]);
					skeleton.Set(HumanBodyBones.Head, bones, neckToHead[1]);
					break;

				default:
					skeleton.Set(HumanBodyBones.Neck, bones, neckToHead[0]);
					// / Find head - prefer bone with "head" in name, otherwise use last bone
					var head = neckToHead.FirstOrDefault(x => x.Name.ToLower().Contains("head")) ?? neckToHead.Last();
					skeleton.Set(HumanBodyBones.Head, bones, head);
					// skeleton.Set(HumanBodyBones.Head, bones, neckToHead.Where(x => x.Parent.Children.Count == 1).Last());
					break;
			}

			skeleton.Set(HumanBodyBones.LeftUpperLeg, bones, legLeft.UpperLeg);
			skeleton.Set(HumanBodyBones.LeftLowerLeg, bones, legLeft.LowerLeg);
			skeleton.Set(HumanBodyBones.LeftFoot, bones, legLeft.Foot);
			skeleton.Set(HumanBodyBones.LeftToes, bones, legLeft.Toes);

			skeleton.Set(HumanBodyBones.RightUpperLeg, bones, legRight.UpperLeg);
			skeleton.Set(HumanBodyBones.RightLowerLeg, bones, legRight.LowerLeg);
			skeleton.Set(HumanBodyBones.RightFoot, bones, legRight.Foot);
			skeleton.Set(HumanBodyBones.RightToes, bones, legRight.Toes);

			skeleton.Set(HumanBodyBones.LeftShoulder, bones, armLeft.Shoulder);
			skeleton.Set(HumanBodyBones.LeftUpperArm, bones, armLeft.UpperArm);
			skeleton.Set(HumanBodyBones.LeftLowerArm, bones, armLeft.LowerArm);
			skeleton.Set(HumanBodyBones.LeftHand, bones, armLeft.Hand);

			skeleton.Set(HumanBodyBones.RightShoulder, bones, armRight.Shoulder);
			skeleton.Set(HumanBodyBones.RightUpperArm, bones, armRight.UpperArm);
			skeleton.Set(HumanBodyBones.RightLowerArm, bones, armRight.LowerArm);
			skeleton.Set(HumanBodyBones.RightHand, bones, armRight.Hand);

			// Validate final mappings
			ValidateSkeletonMappings(skeleton, bones);
			// Verify final mappings
			Debug.Log("Final skeleton mappings:");
			foreach (var bone in skeleton.Bones)
			{
				Debug.Log($"Bone: {bone.Key} -> Index: {bone.Value} -> Name: {bones[bone.Value].Name}");
			}

			return skeleton;
		}

		public Skeleton Detect(Bvh bvh)
		{
			var root = new BvhBone(bvh.Root.Name, Vector3.zero);
			root.Build(bvh.Root);
			return Detect(root.Traverse().Select(x => (IBone)x).ToList());
		}

		public Skeleton Detect(Transform t)
		{
			var root = new BvhBone(t.name, Vector3.zero);
			root.Build(t);
			return Detect(root.Traverse().Select(x => (IBone)x).ToList());
		}

		private void ValidateSkeletonMappings(Skeleton skeleton, IList<IBone> bones)
		{
			// Validate neck and head aren't hand bones
			if (skeleton.Bones.TryGetValue(HumanBodyBones.Head, out int headIndex))
			{
				var headBone = bones[headIndex];
				if (headBone.Name.ToLower().Contains("hand") || headBone.Name.ToLower().Contains("finger"))
				{
					Debug.LogError($"Invalid head mapping detected: {headBone.Name}");
					throw new Exception("Head incorrectly mapped to hand/finger bone");
				}
			}

			// Validate shoulders aren't mapped as neck
			if (skeleton.Bones.TryGetValue(HumanBodyBones.Neck, out int neckIndex))
			{
				var neckBone = bones[neckIndex];
				if (neckBone.Name.ToLower().Contains("shoulder"))
				{
					Debug.LogError($"Invalid neck mapping detected: {neckBone.Name}");
					throw new Exception("Neck incorrectly mapped to shoulder bone");
				}
			}

			// Validate spine chain connections
			ValidateSpineChain(skeleton, bones);
		}

		private void ValidateSpineChain(Skeleton skeleton, IList<IBone> bones)
		{
			// Get spine bones
			var spineBones = new List<HumanBodyBones>
			{
				HumanBodyBones.Spine,
				HumanBodyBones.Chest,
				HumanBodyBones.UpperChest
			};

			IBone previousBone = null;
			foreach (var spineType in spineBones)
			{
				if (skeleton.Bones.TryGetValue(spineType, out int boneIndex))
				{
					var currentBone = bones[boneIndex];

					if (previousBone != null)
					{
						// Verify connection
						if (!currentBone.Traverse().Contains(previousBone) &&
							!previousBone.Traverse().Contains(currentBone))
						{
							Debug.LogError($"Spine chain broken between {previousBone.Name} and {currentBone.Name}");
							throw new Exception("Spine chain is not continuous");
						}
					}

					previousBone = currentBone;
				}
			}
		}
	}
}
