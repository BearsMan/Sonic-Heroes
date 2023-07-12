using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
public class CGTheaterVideoPlayer : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    private float timer;
    // Start is called before the first frame update
    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
    }

    public void Setup(VideoClip clip)
    {
        videoPlayer.clip = clip;
        timer = 0;
        videoPlayer.Play();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.anyKeyDown)
        {
            gameObject.SetActive(false);
        }
        if (!videoPlayer.isPlaying)
        {
            timer += Time.deltaTime;
            if (timer > 0.2f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}