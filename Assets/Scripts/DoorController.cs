using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Gamify.Racing
{
    public class DoorController : MonoBehaviour
    {
        public int doorIndex; // 0, 1, 2, 3

        [Header("Components")]
        public Renderer frameRenderer;
        public Renderer wallRenderer;
        public GameObject wallSolidObject;
        public GameObject indicatorArrow;
        public TextMeshPro badgeText;
        public TextMeshPro optionText;

        [Header("Materials / Colors (Grayscale)")]
        public Color frameDefaultColor = new Color(0.2f, 0.2f, 0.2f);
        public Color frameHighlightColor = Color.white;
        public Color wallDefaultColor = new Color(0.45f, 0.45f, 0.45f);
        public Color wallHighlightColor = new Color(0.85f, 0.85f, 0.85f);
        public Color textDefaultColor = new Color(0.6f, 0.6f, 0.6f);
        public Color textHighlightColor = Color.white;

        [Header("Debris Settings")]
        public int debrisCount = 12;

        private Material _frameMaterial;
        private Material _wallMaterial;
        private List<GameObject> _activeDebris = new List<GameObject>();
        private bool _isBroken = false;
        private bool _isFailed = false;
        private Vector3 _originalWallPos;

        public bool IsBroken => _isBroken;
        public bool IsFailed => _isFailed;

        private void Awake()
        {
            if (frameRenderer != null)
                _frameMaterial = frameRenderer.material;
            if (wallRenderer != null)
                _wallMaterial = wallRenderer.material;
            if (wallSolidObject != null)
                _originalWallPos = wallSolidObject.transform.localPosition;
        }

        public void SetupDoor(int index, string optionString)
        {
            doorIndex = index;
            string[] badges = { "A", "B", "C", "D" };
            string badge = (index >= 0 && index < badges.Length) ? badges[index] : index.ToString();

            if (badgeText != null)
            {
                badgeText.text = $"[{badge}]";
            }

            if (optionText != null)
            {
                optionText.text = optionString;
            }

            ResetDoor();
            SetHighlighted(false);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_isBroken) return;

            if (_frameMaterial != null)
            {
                _frameMaterial.color = highlighted ? frameHighlightColor : frameDefaultColor;
            }

            if (_wallMaterial != null)
            {
                if (_isFailed)
                {
                    _wallMaterial.color = highlighted ? new Color(0.4f, 0.15f, 0.15f) : new Color(0.25f, 0.1f, 0.1f);
                }
                else
                {
                    _wallMaterial.color = highlighted ? wallHighlightColor : wallDefaultColor;
                }
            }

            if (badgeText != null)
            {
                badgeText.color = highlighted ? Color.white : textDefaultColor;
            }

            if (optionText != null)
            {
                optionText.color = highlighted ? Color.white : textDefaultColor;
            }

            if (indicatorArrow != null)
            {
                indicatorArrow.SetActive(highlighted);
            }

            transform.localScale = highlighted ? new Vector3(1.08f, 1.08f, 1.08f) : Vector3.one;
        }

        public void MarkFailed()
        {
            _isFailed = true;
            if (_wallMaterial != null)
            {
                _wallMaterial.color = new Color(0.25f, 0.1f, 0.1f);
            }
            if (badgeText != null)
            {
                badgeText.text = "[X]";
                badgeText.color = new Color(0.8f, 0.3f, 0.3f);
            }
            if (optionText != null)
            {
                optionText.color = new Color(0.55f, 0.4f, 0.4f);
            }
        }

        public void BreakWall()
        {
            if (_isBroken) return;
            _isBroken = true;

            if (wallSolidObject != null)
            {
                wallSolidObject.SetActive(false);
            }

            SpawnDebris();
        }

        public void FlashWallHit()
        {
            StartCoroutine(FlashHitRoutine());
        }

        private IEnumerator FlashHitRoutine()
        {
            if (_wallMaterial == null) yield break;

            Color original = _wallMaterial.color;
            _wallMaterial.color = new Color(1f, 0.3f, 0.3f);

            if (wallSolidObject != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    wallSolidObject.transform.localPosition = _originalWallPos + new Vector3(
                        UnityEngine.Random.Range(-0.1f, 0.1f),
                        UnityEngine.Random.Range(-0.1f, 0.1f),
                        UnityEngine.Random.Range(-0.1f, 0.1f)
                    );
                    yield return new WaitForSeconds(0.02f);
                }
                wallSolidObject.transform.localPosition = _originalWallPos;
            }

            _wallMaterial.color = original;
        }

        public void ResetDoor()
        {
            _isBroken = false;
            _isFailed = false;
            if (wallSolidObject != null)
            {
                wallSolidObject.SetActive(true);
                wallSolidObject.transform.localPosition = _originalWallPos;
            }

            foreach (var d in _activeDebris)
            {
                if (d != null) Destroy(d);
            }
            _activeDebris.Clear();

            transform.localScale = Vector3.one;
        }

        private void SpawnDebris()
        {
            Vector3 center = wallSolidObject != null ? wallSolidObject.transform.position : transform.position;

            for (int i = 0; i < debrisCount; i++)
            {
                GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = $"Debris_{i}";
                chunk.transform.position = center + new Vector3(
                    UnityEngine.Random.Range(-0.8f, 0.8f),
                    UnityEngine.Random.Range(0.2f, 2.0f),
                    UnityEngine.Random.Range(-0.2f, 0.2f)
                );
                chunk.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.2f, 0.45f);

                var mr = chunk.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                mat.color = new Color(UnityEngine.Random.Range(0.3f, 0.8f), UnityEngine.Random.Range(0.3f, 0.8f), UnityEngine.Random.Range(0.3f, 0.8f));
                mr.material = mat;

                var rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                Vector3 blastDir = new Vector3(
                    UnityEngine.Random.Range(-3f, 3f),
                    UnityEngine.Random.Range(2f, 6f),
                    UnityEngine.Random.Range(4f, 10f)
                );
                rb.linearVelocity = blastDir;
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 15f;

                _activeDebris.Add(chunk);
                Destroy(chunk, 2.5f);
            }
        }
    }
}
