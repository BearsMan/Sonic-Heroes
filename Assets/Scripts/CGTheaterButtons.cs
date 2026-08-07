using UnityEngine;
using UnityEngine.Video;
public class CGTheaterButtons : MonoBehaviour
{
    public VideoClip clip;
    public CGTheaterVideoPlayer player;
    public void PlayVideo()
    {
        player.gameObject.SetActive(true);
        player.Setup(clip);
    }

}