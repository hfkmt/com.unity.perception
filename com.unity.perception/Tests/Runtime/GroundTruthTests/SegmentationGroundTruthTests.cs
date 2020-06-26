using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
#if HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;
#endif
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GroundTruthTests
{
    public class ImageReaderBehaviour : MonoBehaviour
    {
        public RenderTexture source;
        public Camera cameraSource;
        RenderTextureReader<uint> m_Reader;

        public event Action<int, NativeArray<uint>> SegmentationImageReceived;

        void Awake()
        {
            m_Reader = new RenderTextureReader<uint>(source, cameraSource, ImageReadCallback);
        }

        void ImageReadCallback(int frameCount, NativeArray<uint> data, RenderTexture renderTexture)
        {
            if (SegmentationImageReceived != null)
                SegmentationImageReceived(frameCount, data);
        }

        void OnDestroy()
        {
            m_Reader.Dispose();
            m_Reader = null;
        }
    }

    //Graphics issues with OpenGL Linux Editor. https://jira.unity3d.com/browse/AISV-422
    [UnityPlatform(exclude = new[] {RuntimePlatform.LinuxEditor, RuntimePlatform.LinuxPlayer})]
    public class SegmentationPassTests : GroundTruthTestBase
    {
        static readonly Color32 k_SemanticPixelValue = Color.blue;

        public enum SegmentationKind
        {
            Instance,
            Semantic
        }
        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator SegmentationPassTestsWithEnumeratorPasses(
            [Values(false, true)] bool useSkinnedMeshRenderer,
            [Values(SegmentationKind.Instance, SegmentationKind.Semantic)] SegmentationKind segmentationKind)
        {
            int timesSegmentationImageReceived = 0;
            int? frameStart = null;
            GameObject cameraObject = null;

            object expectedPixelValue = null;
            void OnSegmentationImageReceived<T>(int frameCount, NativeArray<T> data, RenderTexture tex) where T : struct
            {
                if (frameStart == null || frameStart > frameCount) return;

                timesSegmentationImageReceived++;
                CollectionAssert.AreEqual(Enumerable.Repeat(expectedPixelValue, data.Length), data);
            }

            switch (segmentationKind)
            {
                case SegmentationKind.Instance:
                    expectedPixelValue = 1;
                    cameraObject = SetupCameraInstanceSegmentation(OnSegmentationImageReceived);
                    break;
                case SegmentationKind.Semantic:
                    expectedPixelValue = k_SemanticPixelValue;
                    cameraObject = SetupCameraSemanticSegmentation(OnSegmentationImageReceived);
                    break;
            }

            //
            // // Arbitrary wait for 5 frames for shaders to load. Workaround for issue with Shader.WarmupAllShaders()
            // for (int i=0 ; i<5 ; ++i)
            //     yield return new WaitForSeconds(1);

            frameStart = Time.frameCount;

            //Put a plane in front of the camera
            var planeObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            if (useSkinnedMeshRenderer)
            {
                var oldObject = planeObject;
                planeObject = new GameObject();

                var meshFilter = oldObject.GetComponent<MeshFilter>();
                var meshRenderer = oldObject.GetComponent<MeshRenderer>();
                var skinnedMeshRenderer = planeObject.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.sharedMesh = meshFilter.sharedMesh;
                skinnedMeshRenderer.material = meshRenderer.material;

                Object.DestroyImmediate(oldObject);
            }
            planeObject.transform.SetPositionAndRotation(new Vector3(0, 0, 10), Quaternion.Euler(90, 0, 0));
            planeObject.transform.localScale = new Vector3(10, -1, 10);
            var labeling = planeObject.AddComponent<Labeling>();
            labeling.labels.Add("label");

            AddTestObjectForCleanup(planeObject);

            yield return null;
            yield return null;
            yield return null;
            yield return null;
            //destroy the object to force all pending segmented image readbacks to finish and events to be fired.
            DestroyTestObject(cameraObject);
            DestroyTestObject(planeObject);

            Assert.AreEqual(4, timesSegmentationImageReceived);
        }

        [UnityTest]
        public IEnumerator SegmentationPassProducesCorrectValuesEachFrame(
            [Values(SegmentationKind.Instance, SegmentationKind.Semantic)] SegmentationKind segmentationKind)
        {
            int timesSegmentationImageReceived = 0;
            Dictionary<int, object> expectedLabelAtFrame = null;

            //TestHelper.LoadAndStartRenderDocCapture(out var gameView);

            void OnSegmentationImageReceived<T>(int frameCount, NativeArray<T> data, RenderTexture tex) where T : struct
            {
                if (expectedLabelAtFrame == null || !expectedLabelAtFrame.ContainsKey(frameCount)) return;

                timesSegmentationImageReceived++;

                Debug.Log($"Segmentation image received. FrameCount: {frameCount}");

                try
                {
                    CollectionAssert.AreEqual(Enumerable.Repeat(expectedLabelAtFrame[frameCount], data.Length), data);
                }

                // ReSharper disable once RedundantCatchClause
                catch (Exception)
                {
                    //uncomment to get RenderDoc captures while this check is failing
                    //RenderDoc.EndCaptureRenderDoc(gameView);
                    throw;
                }
            }

            var cameraObject = segmentationKind == SegmentationKind.Instance ?
                SetupCameraInstanceSegmentation(OnSegmentationImageReceived<uint>) :
                SetupCameraSemanticSegmentation(OnSegmentationImageReceived<Color32>);

            object expectedPixelValue = segmentationKind == SegmentationKind.Instance ? (object) 1 : k_SemanticPixelValue;
            expectedLabelAtFrame = new Dictionary<int, object>
            {
                {Time.frameCount    , expectedPixelValue},
                {Time.frameCount + 1, expectedPixelValue},
                {Time.frameCount + 2, expectedPixelValue}
            };
            GameObject planeObject;

            //Put a plane in front of the camera
            planeObject = TestHelper.CreateLabeledPlane();
            yield return null;

            //UnityEditorInternal.RenderDoc.EndCaptureRenderDoc(gameView);
            Object.DestroyImmediate(planeObject);
            planeObject = TestHelper.CreateLabeledPlane();

            //TestHelper.LoadAndStartRenderDocCapture(out gameView);
            yield return null;

            //UnityEditorInternal.RenderDoc.EndCaptureRenderDoc(gameView);
            Object.DestroyImmediate(planeObject);
            planeObject = TestHelper.CreateLabeledPlane();
            yield return null;
            Object.DestroyImmediate(planeObject);
            yield return null;

            //destroy the object to force all pending segmented image readbacks to finish and events to be fired.
            DestroyTestObject(cameraObject);

            Assert.AreEqual(3, timesSegmentationImageReceived);
        }

        GameObject SetupCameraInstanceSegmentation(Action<int, NativeArray<uint>, RenderTexture> onSegmentationImageReceived)
        {
            var cameraObject = SetupCamera(out var perceptionCamera);
            perceptionCamera.InstanceSegmentationImageReadback += onSegmentationImageReceived;
            cameraObject.SetActive(true);
            return cameraObject;
        }

        GameObject SetupCameraSemanticSegmentation(Action<int, NativeArray<Color32>, RenderTexture> onSegmentationImageReceived)
        {
            var cameraObject = SetupCamera(out var perceptionCamera);
            var labelConfig = ScriptableObject.CreateInstance<SemanticSegmentationLabelConfig>();
            labelConfig.LabelEntries = new List<SemanticSegmentationLabelEntry>()
            {
                new SemanticSegmentationLabelEntry()
                {
                    label = "label",
                    color = k_SemanticPixelValue
                }
            };
            var semanticSegmentationLabeler = new SemanticSegmentationLabeler(labelConfig);
            semanticSegmentationLabeler.SemanticSegmentationImageReadback += onSegmentationImageReceived;
            perceptionCamera.labelers.Add(semanticSegmentationLabeler);
            cameraObject.SetActive(true);
            return cameraObject;
        }

        GameObject SetupCamera(out PerceptionCamera perceptionCamera)
        {
            var cameraObject = new GameObject();
            cameraObject.SetActive(false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1;
            perceptionCamera = cameraObject.AddComponent<PerceptionCamera>();
            perceptionCamera.captureRgbImages = false;

            AddTestObjectForCleanup(cameraObject);
            return cameraObject;
        }
    }
}
