// Jit cannot handle more abstract stack setups (anything using the stack to store arbitrary data of unknown length), which defeats the purpose of this.
//namespace WeaverAnnotations.Attributes
//{
//    public class StackAccessAttribute : BaseAttribute { }

//    namespace StackAccess
//    {
//        public static class __stack
//        {
//            public static void Push<T>(T val)
//            {

//            }

//            public static T Pop<T>()
//            {
//                return default;
//            }
//        }
//    }
//}
