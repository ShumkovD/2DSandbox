using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Threading;

public class GridMap : MonoBehaviour
{
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
    public Vector2 tileDimensions;
    public GameObject tilePrefab;
    [Header("Map Generation Information")]
    public float smoothness;
    //�}�b�v�̑傫���ufloat�v
    Vector2 mapSizeF;

    //�}�b�v�̏��
    string[,]   mapID;
    Vector3[,]  tilePositions;
    bool[,]     tileIsRendered;

    //�`��̏��
    [Header("Rendering Information")]
    public Camera cam;
    public int renderingOffset;
    public int additionalMemory = 1;
    [SerializeField] Vector2Int tilesToBeRendered;

    //�J�����̋�
    [SerializeField] Vector2 camLD;
    [SerializeField] Vector2 camUR;
    
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

    public Thread job;
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
        while(functionToRun.Count>0)
        {
            Action temp = functionToRun[0];
            functionToRun.RemoveAt(0);
            temp();
        }
    }

    Action functionToSingleRun;
    public void QueueMainThreadSingleFunction(Action someFunction)
    {
        functionToSingleRun = someFunction;
    }
    public void UseRenewableFunction()
    {
        if(functionToSingleRun != null)
            functionToSingleRun();
        functionToSingleRun = null;
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

        GridMapOnInitialize();
        CameraGetAngles();
        CameraRenderingRegion();
        RenderingPreparation();
        CameraInitializing();

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
        //�}�b�v����
        mapID = CreatingMap();
    }

    string[,] CreatingMap()
    {
        string[,] mapCreated = new string[mapSize.x, mapSize.y];
        //�����A���S���Y��
        for (int x = 0; x < mapSize.x; x++)
        {
            int seed = Mathf.RoundToInt(mapSize.y * Mathf.PerlinNoise(x / smoothness, 0) + 50);
            for (int y = 0; y < mapSize.y; y++)
            {
                if (y < seed)
                {
                    mapCreated[x, y] = "Stone";
                }
                else
                {
                    mapCreated[x, y] = "Grass";
                }
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
        tileIsRendered = new bool[x, y];
    }

    Vector2Int CameraLBIndex(Vector2 cameraLU)
    {
        //�����̃C���f�N�X��T���܂�
        for (int y = 0; y < mapSize.y; y++)
            for (int x = 0; x < mapSize.x; x++)
            {
                if (MyUtility.PointToRectCentral(camLD, tilePositions[x, y], tileDimensions))
                {
                    return new Vector2Int(x, y);
                }
            }
        return Vector2Int.zero;
    }

    Vector2Int CameraLBIndex(Vector2 cameraLU, Vector2Int lbIndex, Vector2Int ruIndex)
    {
        //�����̃C���f�N�X��T���܂�
        for (int y = lbIndex.y; y < ruIndex.y; y++)
            for (int x = lbIndex.x; x < ruIndex.x; x++)
            {
                if (MyUtility.PointToRectCentral(camLD, tilePositions[x, y], tileDimensions))
                {
                    return new Vector2Int(x, y);
                }
            }
        return Vector2Int.zero;
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
            renderedTile[i].tileObject = Instantiate(tilePrefab) as GameObject;
            renderedTile[i].tileObject.SetActive(false);
        }
    }


    GameObject CreateTile(Vector3 tilePosition, Vector2Int index, string id, Vector2Int renderingInfo)
    {
        for (int i = 0; i < renderedTile.Length; i++)
        {
            if (!renderedTile[i].tileObject.activeInHierarchy)
            {
                //�X�v���C�g�̓ǂݍ���

                ChangingSprite(id, i);
                renderedTile[i].tileObject.SetActive(true);
                renderedTile[i].tileObject.transform.position = tilePosition;
                renderedTile[i].index = index;
                tileIsRendered[renderingInfo.x, renderingInfo.y] = true;
                return renderedTile[i].tileObject;

            }
        }
        return null;
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
            for (int o = 0; o < SpriteManager.sprites.Length; o++)
                if (id == SpriteManager.sprites[o].name)
                {
                    renderedTile[i].tileObject.GetComponent<SpriteRenderer>().sprite = SpriteManager.sprites[o];
                }
        }
    }
    /////Tile��
    void ChangingSprite(string id, PooledTile tile)
    {
        if (id != tile.id)
        {
            tile.id = id;
            for (int o = 0; o < SpriteManager.sprites.Length; o++)
                if (id == SpriteManager.sprites[o].name)
                    tile.tileObject.GetComponent<SpriteRenderer>().sprite = SpriteManager.sprites[o];
        }
    }
    ////////////////
    ////////////////
    ////////////////

    GameObject ReCreateTile(Vector3 tilePosition, Vector2Int index, string id, Vector2Int renderingInfo)
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
            ChangingSprite(id, renderedTile[firstUnactive]);
            tileIsRendered[renderingInfo.x, renderingInfo.y] = true;
            renderedTile[firstUnactive].tileObject.SetActive(true);
            renderedTile[firstUnactive].tileObject.transform.position = tilePosition;
            renderedTile[firstUnactive].index = index;
            return renderedTile[firstUnactive].tileObject;
        }
        return null;
    }


    // �f�o�b�O�̂���
    void ClearAllPooledObjects()
    {
        for (int i = 0; i < renderedTile.Length; i++)
        {
            renderedTile[i].tileObject.SetActive(false);
        }
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

    public bool ChunkUpdateCheck(Chunk chunkLD, Vector2Int cameraLD)
    {
        return MyUtility.PointIsInsideOfRect(chunkLD.ld, chunkLD.ru, cameraLD);
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
        CameraGetAngles();
        UseQueuedFunctions();
        UseRenewableFunction();
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
        //�}�b�v�ɂ���^�C���̃C���f�N�X��������
        LBIndex = Vector2Int.zero;
        RUIndex = Vector2Int.zero;


        LBIndex = CameraLBIndex(camLD);
        RUIndex = new Vector2Int(LBIndex.x + cameraRenderingSizeRaw.x, LBIndex.y + cameraRenderingSizeRaw.y);

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

    public void CameraRendering()
    {
        while (true)
        {

            //�����̃C���f�N�X��T���܂�


            LBIndex = CameraLBIndex(camLD, chunkLD.ld, chunkRU.ru);
            if (LBIndex == Vector2Int.zero)
            {
                LBIndex = CameraLBIndex(camLD);
                chunkLD = GetChunkLD(LBIndex - new Vector2Int(renderingOffset, renderingOffset));
                chunkRU = GetChunkRU(RUIndex + new Vector2Int(renderingOffset, renderingOffset));
            }
            RUIndex = new Vector2Int(LBIndex.x + cameraRenderingSizeRaw.x, LBIndex.y + cameraRenderingSizeRaw.y);

            //�O�̃t���[���̃C���f�N�X�Ƃ��̃t���[���̃C���f�N�X�͓�����������A�������܂���
            if (prevLBIndex != LBIndex || prevRUIndex != RUIndex)
            {

                renderingLB = new Vector2(LBIndex.x - renderingOffset, LBIndex.y - renderingOffset);
                renderingRU = new Vector2(RUIndex.x + renderingOffset, RUIndex.y + renderingOffset);

                prevRUIndex = RUIndex;
                prevLBIndex = LBIndex;

                 

                QueueMainThreadSingleFunction(MapRender);
            }
           
        }
    }

    private void MapRender()
    {
        //ClearAllPooledObjects();
        ////�J�����̎���ɑS�Ẵ^�C����ݒ肵����             
        ClearPooledObjectsBasedOnPosition(renderingLB, renderingRU);

        //��ʂɃ^�C����z�u���邱��
        for (int y = LBIndex.y - renderingOffset, yRender = 0; y < RUIndex.y + renderingOffset + 1; y++, yRender++)
            for (int x = LBIndex.x - renderingOffset, xRender = 0; x < RUIndex.x + renderingOffset + 1; x++, xRender++)
            {
                tileIsRendered[xRender, yRender] = false;
                if (!(y >= LBIndex.y && y <= RUIndex.y &&
                    x >= LBIndex.x && x <= RUIndex.x))
                {
                    ReCreateTile(tilePositions[x, y], new Vector2Int(x, y), mapID[x, y], new Vector2Int(xRender, yRender));
            }
    }
        CheckAllrender();
    }

    void CheckAllrender()
    {
        for (int y = LBIndex.y - renderingOffset, yRender = 0; y < RUIndex.y + renderingOffset + 1; y++, yRender++)
            for (int x = LBIndex.x - renderingOffset, xRender = 0; x < RUIndex.x + renderingOffset + 1; x++, xRender++)
            {
                if (!tileIsRendered[xRender, yRender])
                    ReCreateTile(tilePositions[x, y], new Vector2Int(x, y), mapID[x, y], new Vector2Int(xRender, yRender));
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
            Gizmos.DrawLine(tilePositions[0, y] - (Vector3)tileDimensions, tilePositions[mapSize.x - 1, y] - (Vector3)tileDimensions);

        }
        for (int x = 0; x < mapSize.x; x++)
        {
            if (x % chunkSize.x == 0)
                Gizmos.color = Color.red;
            else Gizmos.color = Color.white;
            Gizmos.DrawLine(tilePositions[x, 0] - (Vector3)tileDimensions, tilePositions[x, mapSize.y - 1] - (Vector3)tileDimensions);
        }

        Gizmos.DrawSphere(renderingRU, 0.1f);
        Gizmos.DrawSphere(renderingLB, 0.1f);
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(camUR, 0.1f);
        Gizmos.DrawSphere(camLD, 0.1f);
    }
}
