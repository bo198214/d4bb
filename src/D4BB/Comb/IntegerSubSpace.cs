namespace D4BB.Comb
{
    //this is a quick hack
    public class IntegerSubSpace : IntegerCell
    {
        public IntegerSubSpace(IntegerCell dloc) : base(dloc)
        {

            //origin has to be orthogonal to spat/span
            foreach (int a in dloc.span)
            {
                origin[a] = 0;
            }
        }

        //origin has to be orthogonal to spat/span
        override public IntegerCell Clone()
        {
            return new IntegerSubSpace(this);
        }
    }
}