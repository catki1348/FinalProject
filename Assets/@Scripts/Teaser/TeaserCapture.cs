// AI work record: Claude Sonnet 5.5; 2026-10-18 (created), 2026-10-19 (added BoundsProvider for continuous multi-area shooting). Teaser trailer capture tool (Play Mode only, no game logic).
using System.Collections;
using System.IO;
using UnityEngine;

// 티저 촬영용: Play 모드에서 메인 카메라를 고정 해상도 RenderTexture로 렌더해 이미지로 저장한다.
// - Begin: 연속 프레임(JPG) 저장. Time.captureFramerate로 게임 시간을 프레임 단위로 고정한다.
// - Snapshot: 호출한 프레임 끝에 한 장(PNG)만 저장한다(정지 이미지 촬영용).
// 게임 로직을 바꾸지 않으며, 렌더하는 순간에만 카메라 크기/위치를 바꾸고 바로 되돌린다.
public class TeaserCapture : MonoBehaviour
{
    private RenderTexture target;     // 카메라를 렌더할 출력 텍스처(필요할 때 한 번 만들고 재사용)
    private Texture2D readback;       // GPU 결과를 CPU로 읽어 오는 텍스처(프레임마다 재사용해 할당을 피한다)
    private int frameIndex;           // 지금까지 저장한 프레임 수

    // 촬영용 확대 배율(1 = 게임 카메라 그대로, 0.7 = 70% 크기로 가까이). 렌더하는 순간에만 적용하고 바로 되돌린다.
    public float Zoom { get; set; } = 1f;

    // 지정하면 렌더하는 순간에만 카메라를 이 대상 위치(+오프셋)로 옮겨 찍는다. 넓은 구역에서 인물을 크게 담을 때 쓴다.
    public Transform FollowTarget { get; set; }
    public Vector2 FollowOffset { get; set; } = new Vector2(0f, 2f);

    // 켜면 추적 카메라가 이 영역(월드 좌표) 밖의 빈 배경을 비추지 않도록 화면 가장자리를 영역 안으로 제한한다.
    public bool ClampToBounds { get; set; }
    public Bounds ClampBounds { get; set; }

    // 지정하면 렌더 직전에 호출해 ClampBounds를 갱신한다(카메라 구간이 바뀌는 연속 촬영용, 2026-10-19 추가).
    public System.Func<Bounds> BoundsProvider { get; set; }

    public bool Finished { get; private set; }   // 연속 촬영이 끝났는지 여부(외부 조회용)
    public int FramesWritten => frameIndex;      // 저장한 프레임 수(외부 조회용)

    // 연속 촬영을 시작한다. directory에 frame_00000.jpg부터 저장하고 seconds초 분량(fps 기준)이 되면 멈춘다.
    public void Begin(string directory, float seconds, int fps, int width, int height)
    {
        Directory.CreateDirectory(directory);
        StartCoroutine(Record(directory, seconds, fps, width, height));
    }

    // 지금 프레임의 화면을 PNG 한 장으로 저장한다. 반드시 프레임 끝(WaitForEndOfFrame 이후)에 호출해야 한다.
    public bool Snapshot(string path, int width = 1920, int height = 1080)
    {
        if (!RenderFrame(width, height)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, readback.EncodeToPNG());
        return true;
    }

    // 렌더용 텍스처를 준비한다. 크기가 바뀌면 다시 만든다.
    private void EnsureBuffers(int width, int height)
    {
        if (target != null && target.width == width && target.height == height) return;
        if (target != null) Destroy(target);
        if (readback != null) Destroy(readback);
        target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        readback = new Texture2D(width, height, TextureFormat.RGB24, false);
    }

    // 메인 카메라를 줌/추적/영역 제한을 적용해 target에 렌더하고 readback으로 읽어 온다.
    private bool RenderFrame(int width, int height)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        EnsureBuffers(width, height);
        if (BoundsProvider != null) ClampBounds = BoundsProvider();

        RenderTexture previousTarget = cam.targetTexture;
        float previousSize = cam.orthographicSize;
        Vector3 previousPosition = cam.transform.position;
        cam.targetTexture = target;
        if (cam.orthographic) cam.orthographicSize = previousSize * Zoom;
        if (FollowTarget != null)
        {
            float px = FollowTarget.position.x + FollowOffset.x;
            float py = FollowTarget.position.y + FollowOffset.y;
            if (ClampToBounds && cam.orthographic)
            {
                float halfH = cam.orthographicSize;            // 이미 줌이 적용된 렌더용 크기
                float halfW = halfH * ((float)width / height);
                px = ClampAxis(px, ClampBounds.min.x + halfW, ClampBounds.max.x - halfW, ClampBounds.center.x);
                py = ClampAxis(py, ClampBounds.min.y + halfH, ClampBounds.max.y - halfH, ClampBounds.center.y);
            }
            cam.transform.position = new Vector3(px, py, previousPosition.z);
        }
        cam.Render();
        cam.transform.position = previousPosition;
        cam.orthographicSize = previousSize;
        cam.targetTexture = previousTarget;

        RenderTexture.active = target;
        readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        RenderTexture.active = null;
        return true;
    }

    // 값을 [min, max] 안으로 제한한다. 영역이 화면보다 좁아 범위가 뒤집히면 영역 중심에 고정한다.
    private static float ClampAxis(float value, float min, float max, float center)
    {
        return min > max ? center : Mathf.Clamp(value, min, max);
    }

    // 프레임 끝마다 카메라를 렌더해 저장하는 코루틴.
    private IEnumerator Record(string directory, float seconds, int fps, int width, int height)
    {
        int total = Mathf.RoundToInt(seconds * fps);
        Time.captureFramerate = fps;
        frameIndex = 0;
        Finished = false;

        while (frameIndex < total)
        {
            yield return new WaitForEndOfFrame();
            if (!RenderFrame(width, height)) continue;
            File.WriteAllBytes(Path.Combine(directory, "frame_" + frameIndex.ToString("00000") + ".jpg"), readback.EncodeToJPG(95));
            frameIndex++;
        }

        Time.captureFramerate = 0;
        Finished = true;
    }

    // 오브젝트가 사라질 때 렌더 텍스처를 정리한다.
    private void OnDestroy()
    {
        if (target != null) Destroy(target);
        if (readback != null) Destroy(readback);
    }
}
