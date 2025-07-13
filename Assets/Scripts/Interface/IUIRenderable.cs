public interface IUIRenderable
{
        public bool IsUIActive { set; }
        public void InitUI();
        public void ResetUI();
}