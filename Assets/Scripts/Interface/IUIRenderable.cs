public interface IUIRenderable
{
        public bool IsUIActive { set; }
        public void UpdateUI();
        public void ResetUI();
}