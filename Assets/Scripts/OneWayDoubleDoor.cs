using UnityEngine;
using UnityEngine.EventSystems;
namespace SunTemple
{
    [RequireComponent(typeof(SphereCollider))]
    public class OneWayDoubleDoor : SaveableEntity
    {
        [Header("门物体")]
        public Transform leftDoor;
        public Transform rightDoor;
        [Header("旋转设置")]
        public float openAngle = 90f;
        public float openSpeed = 120f;
        [Header("单向门")]
        public bool oneWay = true;
        public bool openFromFront = true;
        [Header("交互")]
        public float interactRadius = 4f;
        [Header("自动关门")]
        public float autoCloseDistance = 3f;

        private bool isOpen;
        private Quaternion leftClosedRot;
        private Quaternion rightClosedRot;
        private Quaternion leftOpenRot;
        private Quaternion rightOpenRot;

        private SphereCollider rangeTrigger;
        private bool playerInRange;
        private GameObject playerObj;
        private CursorManager cursor;
        private bool scriptIsEnabled = true;

        protected override void Start()
        {
            base.Start();
            rangeTrigger = GetComponent<SphereCollider>();
            rangeTrigger.radius = interactRadius;
            rangeTrigger.isTrigger = true;

            if (leftDoor != null) leftClosedRot = leftDoor.localRotation;
            if (rightDoor != null) rightClosedRot = rightDoor.localRotation;

            playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                Debug.LogWarning($"{nameof(OneWayDoubleDoor)} on {gameObject.name}: 找不到Player标签物体", gameObject);
                scriptIsEnabled = false;
            }
            cursor = CursorManager.instance;
        }

        void Update()
        {
            if (!scriptIsEnabled) return;

            // CG播放期间，门脚本全部逻辑暂停，交给Timeline动画控制门
            if (StartGameFlow.IsCGPlaying) return;

            // 鼠标左键交互，增加UI过滤（和Door.cs保持一致，点击UI不触发开门）
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    //点击在UI上，跳过交互
                }
                else
                {
                    TryInteract();
                }
            }

            // 玩家离开超过距离自动关门
            if (isOpen && playerObj != null)
            {
                float dist = Vector3.Distance(transform.position, playerObj.transform.position);
                if (dist > autoCloseDistance)
                {
                    AutoCloseDoor();
                }
            }

            AnimateDoors();
        }

        void TryInteract()
        {
            if (playerObj == null) return;
            Vector3 playerPos = playerObj.transform.position;
            float dist = Vector3.Distance(transform.position, playerPos);
            if (dist > interactRadius) return;

            bool canOperate = true;
            Vector3 toPlayer = (playerPos - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, toPlayer);
            bool playerAtFront = dot > 0;

            //单向限制，关闭状态才生效
            if (oneWay && !isOpen)
            {
                canOperate = openFromFront ? playerAtFront : !playerAtFront;
            }

            if (!canOperate)
            {
                return;
            }

            //朝远离玩家方向开门
            float angle;
            if (playerAtFront)
            {
                angle = -openAngle;
            }
            else
            {
                angle = openAngle;
            }

            leftOpenRot = leftClosedRot * Quaternion.Euler(0, angle, 0);
            rightOpenRot = rightClosedRot * Quaternion.Euler(0, -angle, 0);
            isOpen = !isOpen;
            UpdateCursor();
        }

        void AutoCloseDoor()
        {
            if (!isOpen) return;
            isOpen = false;
            UpdateCursor();
        }

        void AnimateDoors()
        {
            if (leftDoor != null)
            {
                Quaternion target = isOpen ? leftOpenRot : leftClosedRot;
                leftDoor.localRotation = Quaternion.RotateTowards(leftDoor.localRotation, target, openSpeed * Time.deltaTime);
            }
            if (rightDoor != null)
            {
                Quaternion target = isOpen ? rightOpenRot : rightClosedRot;
                rightDoor.localRotation = Quaternion.RotateTowards(rightDoor.localRotation, target, openSpeed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                playerInRange = true;
                UpdateCursor();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                playerInRange = false;
                cursor?.SetCursorToDefault(this);
            }
        }

        void UpdateCursor()
        {
            if (!playerInRange || cursor == null) return;
            if (playerObj == null) return;

            Vector3 playerPos = playerObj.transform.position;
            Vector3 toPlayer = (playerPos - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, toPlayer);
            bool playerAtFront = dot > 0;

            bool canOperate = true;
            if (oneWay && !isOpen)
            {
                canOperate = openFromFront ? playerAtFront : !playerAtFront;
            }

            if (canOperate)
            {
                cursor.SetCursorToDoor(this);
            }
            else
            {
                cursor.SetCursorToLocked(this);
            }
        }

        /// <summary>
        /// 【供CG回调调用】强制瞬间硬关门，无动画，直接恢复关闭旋转，并更新内部状态
        /// </summary>
        public void ForceHardCloseImmediate()
        {
            if (leftDoor != null)
            {
                leftDoor.localRotation = leftClosedRot;
            }
            if (rightDoor != null)
            {
                rightDoor.localRotation = rightClosedRot;
            }
            isOpen = false;
            UpdateCursor();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, autoCloseDistance);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, -transform.forward * 2f);
        }

        #region 存档实现（继承SaveableEntity，和Door.cs完全一致体系）
        public override void SaveData()
        {
            //保存oneWay单向开关状态
            SaveSystem.SaveEntity(saveableID, nameof(oneWay), oneWay);
        }

        public override void LoadData()
        {
            //读取oneWay，读取失败就保留当前Inspector默认值作为兜底
            oneWay = SaveSystem.LoadEntity<bool>(saveableID, nameof(oneWay), oneWay);
        }
        #endregion
    }
}
