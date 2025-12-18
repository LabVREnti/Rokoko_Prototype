using UnityEngine;
using UnityEngine.UI;

public class canvas_controller : MonoBehaviour
{
    [SerializeField] private GameObject model1;
    [SerializeField] private GameObject model2;
    [SerializeField] private GameObject recorderScreen;
    [SerializeField] private GameObject animationScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        model2.SetActive(false);
       
        animationScreen.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ActiveModel1()
    {
        model1.SetActive(true);
        model2.SetActive(false);
    }

    public void ActiveModel2()
    {
        model1.SetActive(false);
        model2.SetActive(true);
    }

    public void ActiveBothModels()
    {
        model1.SetActive(false);
        model2.SetActive(false);
        model1.SetActive(true);
        model2.SetActive(true);
    }

    public void ActiveRecorderScreen()
    {
        recorderScreen.SetActive(true);
        animationScreen.SetActive(false);
    }

    public void ActiveAnimationScreen()
    {
        recorderScreen.SetActive(false);
        animationScreen.SetActive(true);
    }
}
