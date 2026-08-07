using UnityEngine;

public class SectionTrigger : MonoBehaviour
{
    public GameObject nextSection;
    public GameObject previousSection;



    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            nextSection.SetActive(true);
            previousSection.SetActive(false);
        }
    }
}
