using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteManager : MonoBehaviour
{
    static SpriteManager Instance;

    public static Sprite[] sprites;

    // Start is called before the first frame update
    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else Instance = this;

        object[] loadedSprites = Resources.LoadAll("Tiles", typeof(Sprite));
        sprites = new Sprite[loadedSprites.Length];
        loadedSprites.CopyTo(sprites, 0);
    }

    // Update is called once per frame
    
}
