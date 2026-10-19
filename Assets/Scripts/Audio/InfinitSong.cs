using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class InfinitSong : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private AudioMixer m_AudioMixer;
    
    [SerializeField]
    private AudioSource m_AudioSourceA;
    
    [SerializeField]
    private AudioSource m_AudioSourceB;
    [SerializeField]
    private AudioClip m_loop;
    [Header("Configuration")]
    [SerializeField]
    private float m_pitchShiftLoopA = 1.0f;
    [SerializeField]
    private float m_pitchShiftLoopB = 1.0f;
    [SerializeField]
    private float m_offsetPitchShift = 1.0f;
    [SerializeField]
    private int m_loopCount = 4;
    [SerializeField]
    private int m_loopBOffset = 2;
    [SerializeField]
    private int m_loopAIndex = 0;
    [SerializeField]
    private int m_loopBIndex = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(TimerA());
        StartCoroutine(TimerB(m_loop.length * m_loopBOffset));
    }

    IEnumerator TimerA()
    {
        if(m_loopAIndex == m_loopCount)
        {
            m_pitchShiftLoopA = 1;
            m_AudioMixer.SetFloat("PitchLoopA", m_pitchShiftLoopA);
            m_loopAIndex = 0;
        }

        m_pitchShiftLoopA += m_offsetPitchShift;
        m_AudioMixer.SetFloat("PitchLoopA", m_pitchShiftLoopA);
        m_AudioSourceA.PlayOneShot(m_loop);

        m_loopAIndex++;

        yield return new WaitForSeconds(m_loop.length);
        StartCoroutine(TimerA());
    }

    IEnumerator TimerB(float startTimer)
    {
        if (startTimer != 0)
            yield return new WaitForSeconds(startTimer);

        if(m_loopBIndex == m_loopCount)
        {
            m_pitchShiftLoopB = 1;
            m_AudioMixer.SetFloat("PitchLoopB", m_pitchShiftLoopB);
            m_loopAIndex = 0;
        }

        m_pitchShiftLoopB += m_offsetPitchShift;
        m_AudioMixer.SetFloat("PitchLoopB", m_pitchShiftLoopB);
        m_AudioSourceB.PlayOneShot(m_loop);

        m_loopBIndex++;

        yield return new WaitForSeconds(m_loop.length);
        StartCoroutine(TimerB(0));  
    }
}
