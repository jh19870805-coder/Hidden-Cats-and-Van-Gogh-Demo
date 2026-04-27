using System;
using UnityEngine;

namespace HiddenCats.UI
{
    /// <summary>
    /// Plays <c>WinEffect</c> / <c>WinEffect02</c> particle hierarchies under WinPop when the popup is shown
    /// (puzzle clear, speedrun completion, etc.) and stops them when WinPop is hidden.
    /// </summary>
    public sealed class WinPopCelebrationParticles : MonoBehaviour
    {
        private static readonly string[] EffectRootNames = { "WinEffect", "WinEffect02" };

        private ParticleSystem[] _particleSystems;

        private void Awake()
        {
            CacheCelebrationParticles();
            StopCelebrationParticlesImmediate();
        }

        private void OnEnable()
        {
            PlayCelebrationParticles();
        }

        private void OnDisable()
        {
            StopCelebrationParticles();
        }

        private void CacheCelebrationParticles()
        {
            var list = new System.Collections.Generic.List<ParticleSystem>();
            foreach (string n in EffectRootNames)
            {
                Transform t = FindChildTransformByName(transform, n);
                if (t == null)
                {
                    continue;
                }

                list.AddRange(t.GetComponentsInChildren<ParticleSystem>(true));
            }

            _particleSystems = list.ToArray();
        }

        private void PlayCelebrationParticles()
        {
            if (_particleSystems == null || _particleSystems.Length == 0)
            {
                CacheCelebrationParticles();
            }

            foreach (string n in EffectRootNames)
            {
                Transform root = FindChildTransformByName(transform, n);
                if (root != null)
                {
                    root.gameObject.SetActive(true);
                }
            }

            if (_particleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem ps in _particleSystems)
            {
                if (ps == null)
                {
                    continue;
                }

                ps.gameObject.SetActive(true);
                ps.Clear(true);
                ps.Play(true);
            }
        }

        private void StopCelebrationParticles()
        {
            if (_particleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem ps in _particleSystems)
            {
                if (ps == null)
                {
                    continue;
                }

                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            foreach (string n in EffectRootNames)
            {
                Transform root = FindChildTransformByName(transform, n);
                if (root != null)
                {
                    root.gameObject.SetActive(false);
                }
            }
        }

        private void StopCelebrationParticlesImmediate()
        {
            StopCelebrationParticles();
        }

        private static bool TransformNameMatches(string actual, string expected)
        {
            if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            if (actual == expected)
            {
                return true;
            }

            return actual.StartsWith(expected + "(", StringComparison.Ordinal);
        }

        private static Transform FindChildTransformByName(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (TransformNameMatches(root.name, name))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = FindChildTransformByName(root.GetChild(i), name);
                if (c != null)
                {
                    return c;
                }
            }

            return null;
        }
    }
}
