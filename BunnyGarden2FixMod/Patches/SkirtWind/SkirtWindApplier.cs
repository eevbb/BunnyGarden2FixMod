using System.Collections;
using GB.Game;
using GB.Scene;
using UnityEngine;

namespace BunnyGarden2FixMod.Patches.SkirtWind;

public class SkirtWindApplier : MonoBehaviour
{
    private GameObject m_chara;
    private Animator m_animator;
    private CharID m_charId;
    private readonly IResourceLoader<RuntimeAnimatorController> m_animLoader = new AddressableAnimLoader();
    private float m_boneWeight;

    public static void Attach(CharacterHandle handle)
    {
        var chara = handle.m_chara;
        if (chara == null)
        {
            Plugin.Logger.LogWarning($"[SkirtWind] {handle.m_id}のキャラクターが見つかりません");
            return;
        }

        var originalSkeleton = chara.transform.Find("Root_skinJT");
        if (originalSkeleton == null)
        {
            Plugin.Logger.LogWarning($"[SkirtWind] {chara.name}のスケルトンが見つかりません");
            return;
        }

        if (chara.TryGetComponent<Marker>(out var _))
        {
            Plugin.Logger.LogWarning(
                $"[SkirtWind] {chara.name}はすでにMarkerを持っています。スカート揺れの適用をスキップします。");
            return;
        }

        chara.AddComponent<Marker>();

        var obj = new GameObject($"{nameof(SkirtWindApplier)}_{handle.m_id}");
        DontDestroyOnLoad(obj);

        // Initialization
        {
            var applier = obj.AddComponent<SkirtWindApplier>();
            applier.m_chara = chara;
            applier.m_animator = obj.AddComponent<Animator>();
            applier.m_charId = handle.m_id;

            applier.CloneSkeleton(originalSkeleton, obj.transform);
            Plugin.Logger.LogInfo($"[SkirtWind] スケルトンクローンをインスタンス化しました");

            applier.m_animLoader.Load(handle.m_id, new CharacterHandle.LoadArg
            {
                // これはスカート揺れアニメーションを持つアニメーターです。
                AnimatorType = CharacterHandle.AnimatorType.Minigame,
            });
            applier.m_animLoader.OnComplete(() =>
            {
                if (applier == null || !applier.m_animLoader.IsValid())
                    return;

                applier.m_animator.runtimeAnimatorController = applier.m_animLoader.Result();
            });
        }
    }

    private IEnumerator Start()
    {
        // アニメーターが有効になるのを待ちます。
        while (m_animator == null || m_animator.runtimeAnimatorController == null)
            yield return null;

        // 理由は分かりませんが、ここで呼び出す必要があります。
        m_animator.ClearInternalControllerPlayable();
        yield return null;

        m_animator.SetLayerWeight((int)CharacterHandle.Layer.LAYER_FACIAL, 0f);
        m_animator.SetLayerWeight((int)CharacterHandle.Layer.LAYER_EYE, 0f);
        m_animator.SetLayerWeight((int)CharacterHandle.Layer.LAYER_BASE, 0f);
        m_animator.SetLayerWeight((int)CharacterHandle.Layer.LAYER_SKIRT, 1f);
        m_animator.Play("skirt_swaying_lp", 3);
    }

    private Transform CloneSkeleton(Transform source, Transform parent)
    {
        var newParent = new GameObject(source.name).transform;
        newParent.SetParent(parent, false);
        var bone = newParent.gameObject.AddComponent<Bone>();
        bone.target = source;
        bone.applier = this;

        foreach (Transform child in source)
            CloneSkeleton(child, newParent);

        return newParent;
    }

    private void Update()
    {
        if (m_chara == null)
        {
            Destroy(gameObject);
            return;
        }

        var controller = SkirtWindController.Instance;
        if (controller == null)
            return;

        m_boneWeight = controller.GetWeight(m_charId);
        m_animator.speed = controller.GetAnimSpeed();
    }

    private void OnDestroy()
    {
        m_animLoader.Release();
    }

    private class Marker : MonoBehaviour {}

    private class Bone : MonoBehaviour
    {
        public SkirtWindApplier applier;
        public Transform target;

        private void Update()
        {
            // これは厳密には必要ありませんが、厄介なバグを防ぐことができるため、安全のためにここに残しておきます。
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            if (applier == null || target == null || applier.m_boneWeight <= 0f)
                return;

            // 無関係なボーンが影響を受けないようにします。
            // これは完璧な解決策ではありません。ゼロ/ゼロが正当なアニメーションフレームである可能性があるためです。
            // しかし、スカートアニメーションでは実際に機能します。
            if (transform.localPosition == Vector3.zero && transform.localRotation == Quaternion.identity)
                return;

            target.localPosition = Vector3.LerpUnclamped(
                target.localPosition,
                transform.localPosition,
                applier.m_boneWeight);

            target.localRotation = Quaternion.SlerpUnclamped(
                target.localRotation,
                transform.localRotation,
                applier.m_boneWeight);
        }
    }
}