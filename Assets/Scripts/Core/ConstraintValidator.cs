using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ValidationResult
    {
        public List<FaceContact> contacts = new List<FaceContact>();
        public List<KitchenElement> violations = new List<KitchenElement>();
        public List<List<KitchenElement>> isolatedGroups = new List<List<KitchenElement>>();
        public bool isValid;
    }

    public static class ConstraintValidator
    {
        private const float ContactDistMM = 0.5f;
        private const float FaceToFaceOverlap = 0.5f;

        // Якорь графа связности — пол или стена (к ним заземляются доски).
        private static bool IsAnchor(KitchenElement e) =>
            e != null && (e.GetComponent<BasePlate>() != null || e.GetComponent<Wall>() != null);

        public static ValidationResult Validate(List<KitchenElement> all)
        {
            var result = new ValidationResult();

            if (all == null || all.Count == 0)
            {
                result.isValid = true;
                return result;
            }

            float contactDist = ContactDistMM * AppConstants.MM_TO_UNITS;
            var overlapping = new HashSet<KitchenElement>();

            for (int i = 0; i < all.Count; i++)
            {
                for (int j = i + 1; j < all.Count; j++)
                {
                    var a = all[i];
                    var b = all[j];
                    if (a == null || b == null) continue;
                    if (SnapSystem.ElementsIntersect(a, b))
                    {
                        // Пересечение объёмов физически недопустимо: две доски не могут
                        // занимать одно место. Помечаем обе как нарушение (даже если по
                        // связности они валидны) — это и есть «красный» при перетаскивании.
                        overlapping.Add(a);
                        overlapping.Add(b);
                        continue;
                    }

                    CheckPair(a, b, contactDist, result);
                }
            }

            CheckConnectivity(all, result);

            // Пересекающиеся доски добавляем к нарушениям поверх проверки связности
            // (BasePlate исключаем — он якорь, его «пересечения» с досками — это контакт).
            foreach (var e in overlapping)
            {
                if (e == null || IsAnchor(e)) continue;
                if (!result.violations.Contains(e))
                    result.violations.Add(e);
            }
            result.isValid = result.violations.Count == 0;

            return result;
        }

        private static void CheckPair(KitchenElement a, KitchenElement b, float contactDist, ValidationResult result)
        {
            var facesA = a.GetFaces();
            var facesB = b.GetFaces();

            for (int fa = 0; fa < 6; fa++)
            {
                for (int fb = 0; fb < 6; fb++)
                {
                    float dot = Vector3.Dot(facesA[fa].normal, facesB[fb].normal);
                    if (Mathf.Abs(dot) < 0.999f) continue;

                    Vector3 offset = facesB[fb].center - facesA[fa].center;
                    float planeDist = Mathf.Abs(Vector3.Dot(offset, facesA[fa].normal));
                    if (planeDist > contactDist) continue;

                    if (!FacesOverlap(facesA[fa], facesB[fb], out float overlapArea, out float overlapRatio))
                        continue;

                    bool faceToFace = overlapRatio >= FaceToFaceOverlap;
                    result.contacts.Add(new FaceContact(a, b, fa, fb, overlapArea, faceToFace));
                }
            }
        }

        private static bool FacesOverlap(
            KitchenElement.Face a, KitchenElement.Face b,
            out float overlapArea, out float overlapRatio)
        {
            Vector3 u = a.rightAxis;
            Vector3 v = a.upAxis;

            Rect aRect = GetFaceRect(a, u, v);
            Rect bRect = GetFaceRect(b, u, v);

            float interLeft = Mathf.Max(aRect.xMin, bRect.xMin);
            float interRight = Mathf.Min(aRect.xMax, bRect.xMax);
            float interBottom = Mathf.Max(aRect.yMin, bRect.yMin);
            float interTop = Mathf.Min(aRect.yMax, bRect.yMax);

            if (interLeft >= interRight || interBottom >= interTop)
            {
                overlapArea = 0;
                overlapRatio = 0;
                return false;
            }

            overlapArea = (interRight - interLeft) * (interTop - interBottom);
            float minArea = Mathf.Min(aRect.width * aRect.height, bRect.width * bRect.height);
            overlapRatio = minArea > 0 ? overlapArea / minArea : 0;
            return true;
        }

        private static Rect GetFaceRect(KitchenElement.Face face, Vector3 u, Vector3 v)
        {
            Vector2 center = new Vector2(
                Vector3.Dot(face.center, u),
                Vector3.Dot(face.center, v)
            );

            float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                       + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
            float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                       + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;

            return new Rect(center.x - halfU, center.y - halfV, halfU * 2, halfV * 2);
        }

        private static void CheckConnectivity(List<KitchenElement> all, ValidationResult result)
        {
            var adjacency = new Dictionary<KitchenElement, List<KitchenElement>>();
            var hasContact = new HashSet<KitchenElement>();

            foreach (var e in all)
                adjacency[e] = new List<KitchenElement>();

            foreach (var contact in result.contacts)
            {
                if (!contact.isFaceToFace) continue;
                hasContact.Add(contact.elementA);
                hasContact.Add(contact.elementB);
                adjacency[contact.elementA].Add(contact.elementB);
                adjacency[contact.elementB].Add(contact.elementA);
            }

            var visited = new HashSet<KitchenElement>();
            var queue = new Queue<KitchenElement>();

            // Якоря (пол и стены) — корни BFS: всё пристыкованное к ним заземлено.
            foreach (var e in all)
            {
                if (IsAnchor(e))
                {
                    visited.Add(e);
                    queue.Enqueue(e);
                }
            }

            if (queue.Count == 0)
            {
                KitchenElement start = null;
                foreach (var e in all)
                    if (hasContact.Contains(e)) { start = e; break; }
                if (start == null) start = all[0];
                visited.Add(start);
                queue.Enqueue(start);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!adjacency.TryGetValue(current, out var neighbors)) continue;
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            foreach (var e in all)
            {
                if (IsAnchor(e)) continue;
                if (!visited.Contains(e) || !hasContact.Contains(e))
                    result.violations.Add(e);
            }

            var unvisited = new List<KitchenElement>(result.violations);
            while (unvisited.Count > 0)
            {
                var group = new List<KitchenElement>();
                var gq = new Queue<KitchenElement>();
                gq.Enqueue(unvisited[0]);

                while (gq.Count > 0)
                {
                    var current = gq.Dequeue();
                    if (!unvisited.Contains(current)) continue;
                    unvisited.Remove(current);
                    group.Add(current);

                    if (!adjacency.TryGetValue(current, out var neighbors)) continue;
                    foreach (var neighbor in neighbors)
                    {
                        if (unvisited.Contains(neighbor) && !group.Contains(neighbor))
                            gq.Enqueue(neighbor);
                    }
                }

                if (group.Count > 0)
                    result.isolatedGroups.Add(group);
            }

            result.isValid = result.violations.Count == 0;
        }
    }
}
