//using Snork.EntityFrameworkTools;

//namespace Hangfire.EntityFrameworkStorage
//{
//    internal class PrefixRenamer : IObjectRenamer
//    {
//        public PrefixRenamer(string prefix)
//        {
//            Prefix = prefix;
//        }

//        public string Prefix { get; }

//        public string Rename(ObjectTypeEnum type, string name)
//        {
//            if (Prefix.Equals(EntityFrameworkStorageOptions.DefaultTablePrefix))
//            {
//                switch (type)
//                {
//                    case ObjectTypeEnum.Table:
//                        return string.Concat(Prefix, name);
//                    default:
//                        return name;
//                }
//            }
//            else
//            {
//                return string.Concat(Prefix, name);
//            }

//        }
//    }
//}

