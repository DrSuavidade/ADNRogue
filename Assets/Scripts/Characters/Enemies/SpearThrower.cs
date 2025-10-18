using UnityEngine;

public class SpearThrower : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public Transform rightHand;     // mixamorig:RightHand (só referência)
    public Transform spearSocket;   // Empty filho da mão
    public GameObject spearPrefab;  // Prefab com Rigidbody+Collider+SpearProjectile
    public Transform throwOrigin;   // Empty à frente da mão (direção)

    [Header("Throw")]
    public float throwForce = 30f;

    GameObject heldSpear;
    Collider[] ownerCols;           // cache para IgnoreCollision

    void Start()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        ownerCols = GetComponentsInChildren<Collider>(true);
        SpawnHeldSpear();
    }

    void OnDisable()
    {
        CancelInvoke(nameof(SpawnHeldSpear));
    }

    void SpawnHeldSpear()
    {
        if (!spearPrefab || !spearSocket)
        {
            Debug.LogError("[SpearThrower] SpearPrefab/SpearSocket em falta.");
            return;
        }

        // 1) limpar qualquer coisa que esteja dentro do socket (evita duplicados)
        for (int i = spearSocket.childCount - 1; i >= 0; i--)
            Destroy(spearSocket.GetChild(i).gameObject);

        // 2) se já temos referência, não criar outra
        if (heldSpear != null) return;

        // 3) instanciar e preparar “presa na mão”
        heldSpear = Instantiate(spearPrefab, spearSocket);
        heldSpear.name = "Spear_Held";
        heldSpear.transform.localPosition = Vector3.zero;
        heldSpear.transform.localRotation = Quaternion.identity;

        var rb  = heldSpear.GetComponent<Rigidbody>();
        var col = heldSpear.GetComponent<Collider>();
        if (!rb || !col)
        {
            Debug.LogError("[SpearThrower] O prefab da lança precisa de Rigidbody + Collider + SpearProjectile.");
            return;
        }

        rb.isKinematic = true;   // presa à mão
        col.enabled    = false;  // sem colisão na mão
    }

    public void TryThrow()
    {
        if (heldSpear == null) return;

        if (animator) animator.SetTrigger("Throw"); // só anima
        ThrowNow();                                  // lança já (não depende do Animation Event)
    }

    // Podes ligar este método num Animation Event para sincronizar o “soltar”
    public void OnThrowRelease() => ThrowNow();

    void ThrowNow()
    {
        if (heldSpear == null) return;

        var proj = heldSpear.GetComponent<SpearProjectile>();
        if (!proj)
        {
            Debug.LogError("[SpearThrower] SpearProjectile não encontrado no prefab da lança.");
            return;
        }

        // soltar da mão
        heldSpear.transform.SetParent(null, true);

        // direção: ThrowOrigin -> SpearSocket -> forward do personagem
        Vector3 dir = (throwOrigin ? throwOrigin.forward :
                       (spearSocket ? spearSocket.forward : transform.forward)).normalized;

        // lançar e ignorar colisões com o dono
        proj.Launch(dir * throwForce, ownerCols);

        heldSpear = null;

        // rearmar após pequeno atraso
        Invoke(nameof(SpawnHeldSpear), 0.4f);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryThrow();
    }
}
