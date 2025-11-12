using UnityEngine;

namespace realvirtual
{
    public class CollectionObject : realvirtualBehavior
    {
        public Texture2D
            Icon; //!< Icon of the object in Library, An Icon is not required, if not existing it will be automatically generated while loading the collection

        public string NameInCollection; //!< Name of the object in the collection, if it is empty it is the prefab name
        public string Bundle; //!< Name of the bundle (loadable file)
        public string Asset; //!< Name of the prefab within the bundle
        public bool DisplayName = true;
        public bool PlaceOnBottom = true;
        public bool PlaceOnTopOffOthers;

#if REALVIRTUAL_PLANNER
        [ReadOnly] public CollectionManager CollectionManager;
#endif
    }
}