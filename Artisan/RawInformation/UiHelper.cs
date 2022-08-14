using System;
using System.Runtime.InteropServices;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Artisan.RawInformation
{
    public static unsafe partial class UiHelper
    {
        public static void SetSize(AtkResNode* node, int? width, int? height)
        {
            if (width != null && width >= ushort.MinValue && width <= ushort.MaxValue) node->Width = (ushort)width.Value;
            if (height != null && height >= ushort.MinValue && height <= ushort.MaxValue) node->Height = (ushort)height.Value;
            node->Flags_2 |= 0x1;
        }

        public static void SetPosition(AtkResNode* node, float? x, float? y)
        {
            if (x != null) node->X = x.Value;
            if (y != null) node->Y = y.Value;
            node->Flags_2 |= 0x1;
        }

        public static void SetPosition(AtkUnitBase* atkUnitBase, float? x, float? y)
        {
            if (x >= short.MinValue && x <= short.MaxValue) atkUnitBase->X = (short)x.Value;
            if (y >= short.MinValue && x <= short.MaxValue) atkUnitBase->Y = (short)y.Value;
        }

        public static void SetWindowSize(AtkComponentNode* windowNode, ushort? width, ushort? height)
        {
            if (((AtkUldComponentInfo*)windowNode->Component->UldManager.Objects)->ComponentType != ComponentType.Window) return;

            width ??= windowNode->AtkResNode.Width;
            height ??= windowNode->AtkResNode.Height;

            if (width < 64) width = 64;

            SetSize(windowNode, width, height);  // Window
            var n = windowNode->Component->UldManager.RootNode;
            SetSize(n, width, height);  // Collision
            n = n->PrevSiblingNode;
            SetSize(n, (ushort)(width - 14), null); // Header Collision
            n = n->PrevSiblingNode;
            SetSize(n, width, height); // Background
            n = n->PrevSiblingNode;
            SetSize(n, width, height); // Focused Border
            n = n->PrevSiblingNode;
            SetSize(n, (ushort)(width - 5), null); // Header Node
            n = n->ChildNode;
            SetSize(n, (ushort)(width - 20), null); // Header Seperator
            n = n->PrevSiblingNode;
            SetPosition(n, width - 33, 6); // Close Button
            n = n->PrevSiblingNode;
            SetPosition(n, width - 47, 8); // Gear Button
            n = n->PrevSiblingNode;
            SetPosition(n, width - 61, 8); // Help Button

            windowNode->AtkResNode.Flags_2 |= 0x1;
        }

        public static void ExpandNodeList(AtkComponentNode* componentNode, ushort addSize)
        {
            var newNodeList = ExpandNodeList(componentNode->Component->UldManager.NodeList, componentNode->Component->UldManager.NodeListCount, (ushort)(componentNode->Component->UldManager.NodeListCount + addSize));
            componentNode->Component->UldManager.NodeList = newNodeList;
        }

        public static void ExpandNodeList(AtkUnitBase* atkUnitBase, ushort addSize)
        {
            var newNodeList = ExpandNodeList(atkUnitBase->UldManager.NodeList, atkUnitBase->UldManager.NodeListCount, (ushort)(atkUnitBase->UldManager.NodeListCount + addSize));
            atkUnitBase->UldManager.NodeList = newNodeList;
        }

        private static AtkResNode** ExpandNodeList(AtkResNode** originalList, ushort originalSize, ushort newSize = 0)
        {
            if (newSize <= originalSize) newSize = (ushort)(originalSize + 1);
            var oldListPtr = new IntPtr(originalList);
            var newListPtr = Alloc((ulong)((newSize + 1) * 8));
            var clone = new IntPtr[originalSize];
            Marshal.Copy(oldListPtr, clone, 0, originalSize);
            Marshal.Copy(clone, 0, newListPtr, originalSize);
            return (AtkResNode**)(newListPtr);
        }

        public static AtkResNode* CloneNode(AtkResNode* original)
        {
            var size = original->Type switch
            {
                NodeType.Res => sizeof(AtkResNode),
                NodeType.Image => sizeof(AtkImageNode),
                NodeType.Text => sizeof(AtkTextNode),
                NodeType.NineGrid => sizeof(AtkNineGridNode),
                NodeType.Counter => sizeof(AtkCounterNode),
                NodeType.Collision => sizeof(AtkCollisionNode),
                _ => throw new Exception($"Unsupported Type: {original->Type}")
            };

            var allocation = Alloc((ulong)size);
            var bytes = new byte[size];
            Dalamud.Logging.PluginLog.Debug($"{allocation}");
            Marshal.Copy(new IntPtr(original), bytes, 0, bytes.Length);
            Marshal.Copy(bytes, 0, allocation, bytes.Length);

            var newNode = (AtkResNode*)allocation;
            newNode->ParentNode = null;
            newNode->ChildNode = null;
            newNode->ChildCount = 0;
            newNode->PrevSiblingNode = null;
            newNode->NextSiblingNode = null;
            return newNode;
        }

        public static void Close(AtkUnitBase* atkUnitBase, bool unknownBool = false)
        {
            if (!Ready) return;
            _atkUnitBaseClose(atkUnitBase, (byte)(unknownBool ? 1 : 0));
        }
    }

    public unsafe partial class UiHelper
    {
        private delegate IntPtr GameAlloc(ulong size, IntPtr unk, IntPtr allocator, IntPtr alignment);
        private static GameAlloc _gameAlloc;

        private delegate IntPtr GetGameAllocator();
        private static GetGameAllocator _getGameAllocator;

        private delegate byte AtkUnitBaseClose(AtkUnitBase* unitBase, byte a2);
        private static AtkUnitBaseClose _atkUnitBaseClose;

        public static bool Ready = false;

        public static void Setup(SigScanner scanner)
        {
            _gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(scanner.ScanText("E8 ?? ?? ?? ?? 49 83 CF FF 4C 8B F0"));
            _getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(scanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08"));
            _atkUnitBaseClose = Marshal.GetDelegateForFunctionPointer<AtkUnitBaseClose>(scanner.ScanText("40 53 48 83 EC 50 81 A1"));
            Ready = true;
        }

        public static IntPtr Alloc(ulong size)
        {
            if (_gameAlloc == null || _getGameAllocator == null) return IntPtr.Zero;
            return _gameAlloc(size, IntPtr.Zero, _getGameAllocator(), IntPtr.Zero);
        }

        public static IntPtr Alloc(int size)
        {
            if (size <= 0) throw new ArgumentException("Allocation size must be positive.");
            return Alloc((ulong)size);
        }

    }

    public static unsafe partial class UiHelper
    {
        public static void SetSize<T>(T* node, int? w, int? h) where T : unmanaged => SetSize((AtkResNode*)node, w, h);
        public static void SetPosition<T>(T* node, float? x, float? y) where T : unmanaged => SetPosition((AtkResNode*)node, x, y);
        public static T* CloneNode<T>(T* original) where T : unmanaged => (T*)CloneNode((AtkResNode*)original);
    }
}
