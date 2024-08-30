using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VRM.SpringBone
{
    /// <summary>
    /// 同じ設定のスプリングをまとめて処理する。
    /// 
    /// root o-o-o-x tail
    /// 
    /// [vrm0] tail は 7cm 遠にダミーの joint があるようにふるまう。
    /// 
    /// </summary>
    class SpringBoneSystem
    {
        Dictionary<Transform, Quaternion> m_initialLocalRotationMap;
        List<(Transform, SpringBoneJointInit, JointState)> m_joints = new();
        List<SphereCollider> m_colliders = new();

        public void Setup(SceneInfo scene, bool force)
        {
            if (force || m_initialLocalRotationMap == null)
            {
                m_initialLocalRotationMap = new Dictionary<Transform, Quaternion>();
            }
            else
            {
                foreach (var kv in m_initialLocalRotationMap) kv.Key.localRotation = kv.Value;
                m_initialLocalRotationMap.Clear();
            }
            m_joints.Clear();

            foreach (var go in scene.RootBones)
            {
                if (go != null)
                {
                    foreach (var x in go.transform.GetComponentsInChildren<Transform>(true)) m_initialLocalRotationMap[x] = x.localRotation;

                    SetupRecursive(scene.Center, go);
                }
            }
        }

        private static IEnumerable<Transform> GetChildren(Transform parent)
        {
            for (var i = 0; i < parent.childCount; ++i) yield return parent.GetChild(i);
        }

        private void SetupRecursive(Transform center, Transform parent)
        {
            Vector3 localPosition = default;
            Vector3 scale = default;
            if (parent.childCount == 0)
            {
                // 子ノードが無い。7cm 固定
                var delta = parent.position - parent.parent.position;
                var childPosition = parent.position + delta.normalized * 0.07f * parent.UniformedLossyScale();
                localPosition = parent.worldToLocalMatrix.MultiplyPoint(childPosition); // cancel scale
                scale = parent.lossyScale;
            }
            else
            {
                var firstChild = GetChildren(parent).First();
                localPosition = firstChild.localPosition;
                scale = firstChild.lossyScale;
            }

            var localChildPosition = new Vector3(
                        localPosition.x * scale.x,
                        localPosition.y * scale.y,
                        localPosition.z * scale.z
                    );
            m_joints.Add((
                parent,
                new SpringBoneJointInit
                {
                    LocalRotation = parent.localRotation,
                    BoneAxis = localChildPosition.normalized,
                    Length = localChildPosition.magnitude,
                },
                JointState.Init(center, parent, localChildPosition)));

            foreach (Transform child in parent) SetupRecursive(center, child);
        }

        public void UpdateProcess(float deltaTime,
            SceneInfo scene,
            SpringBoneSettings settings
            )
        {
            if (m_joints == null || m_joints.Count == 0)
            {
                if (scene.RootBones == null) return;
                Setup(scene, false);
            }

            m_colliders.Clear();
            if (scene.ColliderGroups != null)
            {
                foreach (var group in scene.ColliderGroups)
                {
                    if (group != null)
                    {
                        foreach (var collider in group.Colliders)
                        {
                            m_colliders.Add(new SphereCollider(group.transform, collider));
                        }
                    }
                }
            }

            for (int i = 0; i < m_joints.Count; ++i)
            {
                var (transform, init, state) = m_joints[i];
                var nextState = init.Update(deltaTime, scene.Center, transform, settings, m_colliders, state);
                m_joints[i] = (transform, init, nextState);

                //回転を適用
                var r = init.CalcRotation(transform, nextState.CurrentTail);
                transform.rotation = r;
            }
        }

        public void PlayingGizmo(Transform m_center, SpringBoneSettings settings, Color m_gizmoColor)
        {
            foreach (var (transform, init, state) in m_joints)
            {
                init.DrawGizmo(m_center, transform, settings, m_gizmoColor, state);
            }
        }

        public void EditorGizmo(Transform head, float m_hitRadius)
        {
            Vector3 childPosition;
            Vector3 scale;
            if (head.childCount == 0)
            {
                // 子ノードが無い。7cm 固定
                var delta = head.position - head.parent.position;
                childPosition = head.position + delta.normalized * 0.07f * head.UniformedLossyScale();
                scale = head.lossyScale;
            }
            else
            {
                var firstChild = GetChildren(head).First();
                childPosition = firstChild.position;
                scale = firstChild.lossyScale;
            }

            Gizmos.DrawLine(head.position, childPosition);
            Gizmos.DrawWireSphere(childPosition, m_hitRadius * scale.x);

            foreach (Transform child in head) EditorGizmo(child, m_hitRadius);
        }
    }
}