namespace UnityTest
{
    using System;

    using UnityEngine;

    public static class Test
    {
        public static GameObject _obj => new GameObject();
        public static void Testing()
        {

        }
        public static String Proper1()
        {
            var obj = _obj;
            if(obj) return obj.name;
            return null;
        }
        public static String Proper2()
        {
            var obj = _obj;
            if(!obj) return null;
            return obj.name;
        }
        public static String Proper3()
        {
            var obj = _obj;
            if(obj != null) return obj.name;
            return null;
        }
        public static String Proper4()
        {
            var obj = _obj;
            if(obj == null) return null;
            return obj.name;
        }


        public static String ImProper1()
        {
            var obj = _obj;
            if(obj is not null) return obj.name;
            return null;
        }
        public static String ImProper2()
        {
            var obj = _obj;
            if(obj is null) return null;
            return obj.name;
        }
        public static String ImProper3()
        {
            var obj = _obj;
            return obj?.name;
        }
        public static String ImProper4()
        {
            if(_obj is GameObject obj) return obj.name;
            return null;
        }
        public static String ImProper5()
        {
            if(_obj is not GameObject obj) return null;
            return obj.name;
        }
    }
    
}
