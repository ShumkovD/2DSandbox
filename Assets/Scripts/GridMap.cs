using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Threading;

public class GridMap : MonoBehaviour
{
    public static GridMap Instance;
    private void Awake()
    {
        if (Instance != null && this != Instance)
            Destroy(this);
        else Instance = this;
    }

    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// �ϐ��@�@ /// /// /// /// /// /// /// /// /// // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    ///    /// ///
    /// 

    [Header("Map Size")]
    public           Vector2Int mapSize;
    public           Vector2Int chunkSize;
    [SerializeField] Vector2Int chunkAmount;
    [Header("Tile Information")]
    public GameObject parentForCollision;
    public Vector2 tileDimensions;
    public GameObject tilePrefab;
    [Header("Map Generation Information")]
    public float smoothness;
    //�}�b�v�̑傫���ufloat�v
    Vector2 mapSizeF;

    //�}�b�v�̏��
    string [,]  mapID;
    static bool   [,]  mapSolid;
    Vector3[,]  tilePositions;
    bool   [,]  tileIsRendered;

    //�`��̏��
    [Header("Rendering Information")]
    public Camera cam;
    public int renderingOffset;
    public int additionalMemory = 1;
    [SerializeField] Vector2Int tilesToBeRendered;

    //�J�����̋�
    Vector2 camLD;
    Vector2 camUR;
    
    /// 
    /// �`�����N�X�\����
    /// 
    public struct Chunk
    {
        public Vector2Int ld;
        public Vector2Int ru;
    };
    public Chunk[,] chunk;

    /// 
    /// �^�C���\����
    /// 

    struct PooledTile
    {
        public GameObject tileObject;
        public Vector2Int index;
        public string id;
        public SpriteRenderer tileSprite;
        public BoxCollider2D tileCollider;
        public int pooledObjectIndex;
    };

    //�I�u�W�F�N�g�v�[�����O�u�^�C���v
    static PooledTile[] renderedTile;



    /// 
    /// /// 
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// �֐��@�@ /// /// /// /// /// /// /// /// /// // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 


    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// �}���`�X���b�h�@�@ /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /

    List<Action> functionToRun;

    void StartThreadedOperation(Action someFunction)
    {
        Thread newThread = new Thread(new ThreadStart(someFunction));
        newThread.Start();
    }

    public void QueueMainThreadFunction(Action someFunction)
    {
        functionToRun.Add(someFunction);
    }
    public void UseQueuedFunctions()
    {

        while (functionToRun.Count>0)
        {
            
            Action temp = functionToRun[0];
            functionToRun.RemoveAt(0);
            temp();
        }
    }
    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// �X�^�[�g�@�@ /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 
    //�v���O�����̏�����
    void Start()
    {
        functionToRun = new List<Action>();
        //�}�b�v����
        GridMapOnInitialize();
        //�J�����̈ʒu��Ⴄ
        CameraGetAngles();
        //�J�����͈̔͂��v�Z����
        CameraRenderingRegion();
        //�I�u�W�F�N�g�v�[���̏�����
        RenderingPreparation();
        //�J�����ɋ���ł���}�b�v�̕`��
        CameraInitializing();
        //�V�����X���b�h�ŃJ�����̌v�Z������
        StartThreadedOperation(CameraRendering);
    }
    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// �O���b�h�}�b�v�@�@ /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 

    //�^�C���}�b�v�̏������������܂�
    void GridMapOnInitialize()
    {
        mapID =         new string [mapSize.x, mapSize.y];
        mapSolid =      new bool   [mapSize.x, mapSize.y];
        tilePositions = new Vector3[mapSize.x, mapSize.y];

        chunkAmount = new Vector2Int(mapSize.x / chunkSize.x, mapSize.y/chunkSize.y);
        chunk = new Chunk[chunkAmount.x, chunkAmount.y];
        //�^�C���ʒu���v�Z���邽�߂ɁA�}�b�v�̑傫�����v�Z���܂�
        mapSizeF = mapSize * tileDimensions;

        for(int y = 0; y < mapSize.y; y++)
            for(int x = 0; x< mapSize.x; x++)
            {
        //ID�����������܂�
                mapID[x, y] = "";
                mapSolid[x, y] = false;
        //�^�C���̈ʒu���v�Z���܂�
                tilePositions[x, y] = new Vector3(-mapSizeF.x * 0.5f + x * tileDimensions.x, -mapSizeF.y * 0.5f + tileDimensions.y * y);
            }
        //�J�����ʒu�ŁA�`�����N�̕`�揀��
        for (int y = 0, yi = 0; y < mapSize.y; y += chunkSize.y, yi++)
            for (int x = 0, xi = 0; x < mapSize.x; x += chunkSize.x, xi++)
            {

                    chunk[xi, yi].ld = new Vector2Int(x, y);
                    chunk[xi, yi].ru = new Vector2Int(x + chunkSize.x, y + chunkSize.y);
            }
        //�}�b�v�����ubool�v
        mapSolid = CreatingMap();
        //�}�b�v�̕�����
        Smoothing(smoothAmount);
        //�}�b�v�ɂ�ID��^����
        mapID = MapInitID();
        
    }
    [Header("World Map Generation Properties")] //�Z���E�I�[�g�}�g�����g�Ă��܂�
    public int seed;                            //�}�b�v�V�[�h
    public int upperLayerThickness;             //�y�̌���
    [Range(0,100)]
    public int topFillPercent;                  //���A�̔䗦�u�y���C���[�v
    [Range(0,100)]
    public int cavesFillPercent;                //���A�̔䗦�u�΃��C���[�v
    public int smoothAmount;                    //�Z���E�I�[�g�}�g�������񐮗����邩

    int[] mapXHeight;       //�e��̍���
    int[] groundXThickness; //�e��̓y�̌���

    bool[,] CreatingMap()
    {
        //�}�b�v����邽�߂ɏ���
        bool[,] mapCreated = new bool[mapSize.x, mapSize.y];
        groundXThickness = new int[mapSize.x];
        mapXHeight = new int[mapSize.x];
        //�}�b�v�V�[�h
        System.Random caveSeed = new System.Random(seed.GetHashCode());
       
        //�����A���S���Y��
        for (int x = 0; x < mapSize.x; x++)
        {
            //��̍������v�Z����
            int height = Mathf.RoundToInt(mapSize.y * Mathf.PerlinNoise(x / smoothness, 0));
            //���̗�̓y�̌������v�Z����
            int groundThickness = UnityEngine.Random.Range((int)(upperLayerThickness * 0.5f), upperLayerThickness + 1);
            //���̏���ۑ�����
            groundXThickness[x] = groundThickness;
            mapXHeight[x] = height;
            //�̃}�X�N���쐬
            for (int y = 0; y < mapSize.y; y++)
            {
                if (y > height)
                {
                    mapCreated[x, y] = false;
                } 
                else if (y < height - groundThickness)
                {
                    if(caveSeed.Next(0,100)<cavesFillPercent)
                        mapCreated[x, y] = false;
                    else mapCreated[x, y] = true;
                }
                else if(y<height && y >= height - groundThickness)
                {
                    if (caveSeed.Next(0, 100) < topFillPercent)
                        mapCreated[x, y] = false;
                    else mapCreated[x, y] = true;
                }
                
            }
        }
        return mapCreated;
    }
    //�Z���E�I�[�g�}�g���̐���
    void Smoothing(int smoothAmount)
    {
        for (int i = 0; i < smoothAmount; i++)
        {
            for (int x = 0; x < mapSize.x; x++)
            {
                for (int y = 0; y < mapXHeight[x]; y++)
                {
                    int surroudingTiles = GetSurroudingTiles(x, y);
                    if (surroudingTiles > 4)
                    {
                        mapSolid[x, y] = true;
                    }
                    else if (surroudingTiles < 4)
                    {
                        mapSolid[x, y] = false;
                    }
                }
            }
            //���ɂ���^�C�����폜
            for (int x = 0; x < mapSize.x; x++)
            {
                for (int y = 0; y <= mapXHeight[x]; y++)
                {
                    int surroudingTiles = GetSurroudingTiles(x, y);
                    if (surroudingTiles == 0)
                        mapSolid[x, y] = false;
                }
            }
        }
    }

    //����^�C�����v�Z����
    int GetSurroudingTiles(int gridX, int gridY)
    {
        int surroundTiles = 0;
        for(int nx = gridX - 1; nx <= gridX+1; nx++)
            for(int ny = gridY-1;ny<=gridY+1; ny++)
            {
                if(nx>=0 && nx<mapSize.x && ny>=0 &&ny<mapSize.y)
                {
                    if (nx != gridX || ny != gridY)
                    {
                        if (mapSolid[nx, ny])
                        {
                            surroundTiles++;
                        }
                    }
                }
            }
        return surroundTiles;
    }

    int GetSurroudingTilesHorVer(int gridX, int gridY)
    {
        int surroundTiles = 0;
  
        if (mapSolid[gridX - 1, gridY])
              surroundTiles++;
        if (mapSolid[gridX + 1, gridY])
                surroundTiles++;
        if (mapSolid[gridX, gridY + 1])
            surroundTiles++;
        if (mapSolid[gridX, gridY - 1])
            surroundTiles++;
        return surroundTiles;
    }
    //�}�b�v�͌̃}�X�N���g���āA����Ă���B
    string[,] MapInitID()
    {
        string[,] mapCreated = new string[mapSize.x, mapSize.y];
        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                if (mapSolid[x, y])
                {
                    
                    if (y < mapXHeight[x] - groundXThickness[x])
                        mapCreated[x, y] = "Stone";
                    else mapCreated[x, y] = "Grass";
                }
                else mapCreated[x, y] = "";

            }
        }
        return mapCreated;
    }

    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// �`��@�@ /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 

    //�J�����̋��̌v�Z
    void CameraGetAngles()
    {
        camLD = (Vector2)cam.ScreenToWorldPoint(new Vector3(0, 0, cam.transform.position.z));
        camUR = (Vector2)cam.ScreenToWorldPoint(new Vector3(cam.scaledPixelWidth, cam.scaledPixelHeight, cam.transform.position.z));
    }

    //�J�������`�悷��^�C���̐����v�Z���܂�
    Vector2Int cameraRenderingSizeRaw;
    void CameraRenderingRegion()
    {
        //renderingOffset�͉�ʊO�ŉf�����������邽��
        cameraRenderingSizeRaw = new Vector2Int(Mathf.RoundToInt((camUR.x - camLD.x) / tileDimensions.x), Mathf.RoundToInt((camUR.y - camLD.y) / tileDimensions.y));
        int x = Mathf.RoundToInt(cameraRenderingSizeRaw.x + renderingOffset*2+additionalMemory);
        int y = Mathf.RoundToInt(cameraRenderingSizeRaw.y + renderingOffset*2+additionalMemory);
        tilesToBeRendered = new Vector2Int(x, y);
        tileIsRendered = new bool[x + renderingOffset, y + renderingOffset];
    }

    Vector2Int GetTilePosition(Vector2 camLD)
    {
        float x = -0.5f * mapSizeF.x;
        float y = -0.5f * mapSizeF.y;
        return new Vector2Int(-(int)((x - camLD.x) / tileDimensions.x) - 1, -(int)((y - camLD.y) / tileDimensions.y) - 1);
    }

    Vector2Int GetTilePosition(Vector3 pos)
    {
        float x = -0.5f * mapSizeF.x;
        float y = -0.5f * mapSizeF.y;
        return new Vector2Int(-(int)((x - pos.x) / tileDimensions.x) - 1, -(int)((y - pos.y) / tileDimensions.y) - 1);
    }


    /// /////////////////////////////////////////////////////////////////////////////////////////
    /// �^�C���v�[�����O//////////////////////////////////////////////////////////////////////////
    /// /////////////////////////////////////////////////////////////////////////////////////////
    //�����������Ȃ��悤�ɁA�^�C���̓Q�[���ŃI�u�W�F�N�g�v�[�����O���g���Ĕz�u���܂�
    void RenderingPreparation()
    {
        renderedTile = new PooledTile[(tilesToBeRendered.x+1) * (tilesToBeRendered.y+1)];
        for (int i = 0; i < renderedTile.Length; i++)
        {
            renderedTile[i].tileObject = Instantiate(tilePrefab, parentForCollision.transform) as GameObject;
            renderedTile[i].tileCollider = renderedTile[i].tileObject.GetComponent<BoxCollider2D>();
            renderedTile[i].tileCollider.enabled = false;
            renderedTile[i].tileSprite = renderedTile[i].tileObject.GetComponent<SpriteRenderer>();
            renderedTile[i].tileObject.SetActive(false);
            renderedTile[i].pooledObjectIndex = i;
        }
    }

    ////////////////
    //�X�v���C�g�̕ύX
    ////////////////
    /////ID��
    void ChangingSprite(string id, int i)
    {
        if (id != renderedTile[i].id)
        {
            renderedTile[i].id = id;
            if (id == "")
            {
                renderedTile[i].tileSprite.sprite = null;
                return;
            }
            
            for (int o = 0; o < SpriteManager.sprites.Length; o++)
                if (id == SpriteManager.sprites[o].name)
                {
                    renderedTile[i].tileSprite.sprite = SpriteManager.sprites[o];
                    return;
                }
        }
    }

    ////////////////
    ////////////////
    ////////////////


    GameObject CreateTile(Vector3 tilePosition, Vector2Int index, string id, Vector2Int renderingInfo)
    {
        int firstUnactive = 0;
        bool isFound = false;

        if (tileIsRendered[renderingInfo.x, renderingInfo.y])
            return null;

        for (int i = 0; i < renderedTile.Length; i++)
        {
            if (!renderedTile[i].tileObject.activeInHierarchy && !isFound)
            {
                firstUnactive = i;
                isFound = true;
            }

            if (renderedTile[i].index == index)
            {
                int surTile = GetSurroudingTilesHorVer(index.x, index.y);
                if(id == "")
                    renderedTile[i].tileCollider.enabled = false;
                else if (surTile < 4)
                    renderedTile[i].tileCollider.enabled = true;
                else renderedTile[i].tileCollider.enabled = false;

                ChangingSprite(id, i);
                if (!renderedTile[i].tileObject.activeInHierarchy)
                {
                    tileIsRendered[renderingInfo.x, renderingInfo.y] = true;
                    renderedTile[i].tileObject.SetActive(true);
                    return renderedTile[i].tileObject;
                }
                else
                {
                    tileIsRendered[renderingInfo.x, renderingInfo.y] = true;
                    return renderedTile[i].tileObject;
                }
            }
        }
        if (isFound)
        {
            ChangingSprite(id, firstUnactive);
            int surTile = GetSurroudingTilesHorVer(index.x, index.y);
            if (id == "")
                renderedTile[firstUnactive].tileCollider.enabled = false;
            else if (surTile < 4)
                renderedTile[firstUnactive].tileCollider.enabled = true;
            else renderedTile[firstUnactive].tileCollider.enabled = false;
            tileIsRendered[renderingInfo.x, renderingInfo.y] = true;
            renderedTile[firstUnactive].tileObject.SetActive(true);
            renderedTile[firstUnactive].tileObject.transform.position = tilePosition;
            renderedTile[firstUnactive].index = index;
            return renderedTile[firstUnactive].tileObject;
        }
        return null;
    }

    //�`��͈͈ȊO�̃^�C�����I�u�W�F�N�g�v�[���ɖ߂�
    void ClearPooledObjectsBasedOnPosition(Vector2 lb, Vector2 ru)
    {
        for (int i = 0; i < renderedTile.Length; i++)
        {
            if (!renderedTile[i].tileObject.activeInHierarchy)
            {
                continue;
            }
            if (!(renderedTile[i].index.x >= lb.x && renderedTile[i].index.x <= ru.x &&
                  renderedTile[i].index.y >= lb.y && renderedTile[i].index.y <= ru.y))
            {
                renderedTile[i].tileObject.SetActive(false);
                renderedTile[i].tileCollider.enabled = false;
            }
        }

    }

    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// �`�����N�@�@ /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 

    public Chunk GetChunkLD(Vector2Int index)
    {
        for(int y = 0; y < chunkAmount.y; y++)
            for(int x = 0; x < chunkAmount.x; x++)
            {
                if (MyUtility.PointIsInsideOfRect(chunk[x,y].ld, chunk[x,y].ru, index))
                {
                    return chunk[x,y];
                }
            }
        return default;
    }
    public Chunk GetChunkRU(Vector2Int index)
    {
        for (int y = 0; y < chunkAmount.y; y++)
            for (int x = 0; x < chunkAmount.x; x++)
            {
                if (MyUtility.PointIsInsideOfRect(chunk[x, y].ld, chunk[x, y].ru, index))
                {
                    return chunk[x, y];
                }
            }
        return default;
    }

    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// �A�b�v�f�[�g�@�@ /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 

    private void Update()
    {
        //�J�����̏���Ⴄ
        CameraGetAngles();
        //�s��ɓ����Ă���֐�����������
        UseQueuedFunctions();
        if (Input.GetMouseButtonDown(0))
            ChangeTileAtCursor();
    }

    /// /////////////////////////////////////////////////////////////////////////////////////////
    /// �J�����̏���//////////////////////////////////////////////////////////////////////////////
    /// /////////////////////////////////////////////////////////////////////////////////////////

    Vector2Int prevLBIndex;
    Vector2Int prevRUIndex;

    Chunk chunkLD;
    Chunk chunkRU;

    public Vector2Int LBIndex;
    public Vector2Int RUIndex;
    void CameraInitializing()
    {
        
        LBIndex = Vector2Int.zero;
        RUIndex = Vector2Int.zero;
        //�}�b�v�ɂ���^�C���̃C���f�N�X��������
        LBIndex = GetTilePosition(camLD);
        RUIndex = new Vector2Int(LBIndex.x + cameraRenderingSizeRaw.x, LBIndex.y + cameraRenderingSizeRaw.y);
        //�g���Ă���`�����N��������
        chunkLD = GetChunkLD(LBIndex);
        chunkRU = GetChunkRU(RUIndex);

        //��ʂɃ^�C����z�u���邱��
        for (int y = LBIndex.y - renderingOffset, yRendering = 0; y <= RUIndex.y + renderingOffset; y++, yRendering++)
            for (int x = LBIndex.x - renderingOffset, xRendering = 0; x <= RUIndex.x + renderingOffset; x++, xRendering++)
            {
                CreateTile(tilePositions[x, y], new Vector2Int(x, y), mapID[x,y], new Vector2Int(xRendering, yRendering));
            }
        prevRUIndex = RUIndex;
        prevLBIndex = LBIndex;
    }

    //�ʒu�̕ύX���m�肷�邽�߂�
    public Vector2 renderingLB;
    public Vector2 renderingRU;

    //�J�����̌v�Z��ʂ̃X���b�h�ōs��
    public void CameraRendering()
    {

        while (true)
        {
            //�����̃C���f�N�X��T���܂�
            LBIndex = GetTilePosition(camLD);
            RUIndex = new Vector2Int(LBIndex.x + cameraRenderingSizeRaw.x, LBIndex.y + cameraRenderingSizeRaw.y);
            //�`�����N�v�Z
            chunkLD = GetChunkLD(LBIndex - new Vector2Int(renderingOffset, renderingOffset));
            chunkRU = GetChunkRU(RUIndex + new Vector2Int(renderingOffset, renderingOffset));
          

            //�O�̃t���[���̃C���f�N�X�Ƃ��̃t���[���̃C���f�N�X�͓�����������A�������܂���
            if (prevLBIndex != LBIndex || prevRUIndex != RUIndex)
            {
                //�`��͈͂��v�Z����
                renderingLB = new Vector2(LBIndex.x - renderingOffset, LBIndex.y - renderingOffset);
                renderingRU = new Vector2(RUIndex.x + renderingOffset, RUIndex.y + renderingOffset);

                prevRUIndex = RUIndex;
                prevLBIndex = LBIndex;
                //�}�C���X���b�h�ŕ`�悷��
              QueueMainThreadFunction( MapRender);
            }
        }
    }
    //�}�b�v�`��
    private void MapRender()
    {
        ////�J�����̎���ɑS�Ẵ^�C����ݒ肵����             
        ///
            ClearPooledObjectsBasedOnPosition(renderingLB, renderingRU);

        //��ʂɃ^�C����z�u���邱��
        for (int y = LBIndex.y - renderingOffset, yRender = 0; y < RUIndex.y + renderingOffset + 1; y++, yRender++)
            for (int x = LBIndex.x - renderingOffset, xRender = 0; x < RUIndex.x + renderingOffset + 1; x++, xRender++)
            {
                tileIsRendered[xRender, yRender] = false;
                    CreateTile(tilePositions[x, y], new Vector2Int(x, y), mapID[x, y], new Vector2Int(xRender, yRender));

            }
        CheckAllRender();
    }
    //�`��o���Ȃ������ہA�`�悵�܂�
    void CheckAllRender()
    {
        for (int y = LBIndex.y - renderingOffset, yRender = 0; y < RUIndex.y + renderingOffset + 1; y++, yRender++)
            for (int x = LBIndex.x - renderingOffset, xRender = 0; x < RUIndex.x + renderingOffset + 1; x++, xRender++)
            {
                if (!tileIsRendered[xRender, yRender])
                    CreateTile(tilePositions[x, y], new Vector2Int(x, y), mapID[x, y], new Vector2Int(xRender, yRender));
            }
    }

    /// /////////////////
    ///  �^�C���̓���ւ�
    /// /////////////////

    //��
    void ChangeTileAtCursor()
    {
        Vector2Int index;
        index = GetTilePosition(cam.ScreenToWorldPoint(Input.mousePosition) + (Vector3)tileDimensions* 1.5f);

        //���@�[���C���x���g���V�X�e�������܂�
        ChangingTile(index, "");
    }
    //
    void ChangingTile(Vector2Int index, string id)
    {
        int surroundingTile = 0;
        //�^�C���������
        if (id == "")
        {
           
            if (id == mapID[index.x, index.y])
                return;
            mapID[index.x, index.y] = id;
            mapSolid[index.x, index.y] = false;
        }
        else
        {
            //�̃^�C����u����
            if (id == mapID[index.x, index.y])
                return;
            //���ɒu���Ȃ����߂ɁA����̃^�C�����m�F����
            surroundingTile = GetSurroudingTilesHorVer(index.x,index.y);

            if (surroundingTile == 0)
                return;

            mapID[index.x, index.y] = id;
            mapSolid[index.x, index.y] = true;
        }
        //�X�v���C�g��ς���
        for (int i = 0; i < renderedTile.Length; i++)
        {
            if(renderedTile[i].tileObject.activeInHierarchy)
            {
                if (index == renderedTile[i].index)
                {
                    ChangingSprite(id, i);
                    if (id != "")
                        renderedTile[i].tileCollider.enabled = false;
                    else if (surroundingTile < 4)
                        renderedTile[i].tileCollider.enabled = true;
                    else renderedTile[i].tileCollider.enabled = false;

                }
            }

        }
        UpdateSurroundingTiles(index);
    }

    void UpdateSurroundingTiles(Vector2Int index)
    {
        for (int nx = index.x - 1; nx <= index.x + 1; nx++)
            for (int ny = index.y - 1; ny <= index.y + 1; ny++)
            {
                if (nx >= 0 && nx < mapSize.x && ny >= 0 && ny < mapSize.y)
                {
                    for(int i = 0; i < renderedTile.Length; i++)
                    {
                        if (renderedTile[i].tileObject.activeInHierarchy && renderedTile[i].index == new Vector2Int(nx, ny))
                        { 
                            int surTiles = GetSurroudingTilesHorVer(nx, ny);
                            Debug.Log(surTiles);
                            if (surTiles<4 && !(renderedTile[i].id == ""))
                                renderedTile[i].tileCollider.enabled = true;
                            else renderedTile[i].tileCollider.enabled = false;
                        }
                    }

                  
                }
            }
    }

    private void OnDrawGizmos()
    {
        if (tilePositions == null)
            return;
        

        for (int y = 0; y < mapSize.y; y++)
        {
            if(y%chunkSize.y == 0)
                Gizmos.color = Color.red;
            else Gizmos.color = Color.white;
            Gizmos.DrawLine(tilePositions[0, y] - (Vector3)tileDimensions *0.5f, tilePositions[mapSize.x - 1, y] - (Vector3)tileDimensions * 0.5f);

        }
        for (int x = 0; x < mapSize.x; x++)
        {
            if (x % chunkSize.x == 0)
                Gizmos.color = Color.red;
            else Gizmos.color = Color.white;
            Gizmos.DrawLine(tilePositions[x, 0] - (Vector3)tileDimensions * 0.5f, tilePositions[x, mapSize.y - 1] - (Vector3)tileDimensions * 0.5f);
        }
        for (int i = 0; i < renderedTile.Length; i++)
            if (renderedTile[i].tileCollider.isActiveAndEnabled)
                Gizmos.DrawCube(renderedTile[i].tileObject.transform.position, new Vector3(0.8f, 0.8f));
        Gizmos.DrawSphere(renderingRU, 0.1f);
        Gizmos.DrawSphere(renderingLB, 0.1f);
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(camUR, 0.1f);
        Gizmos.DrawSphere(camLD, 0.1f);
    }
}
