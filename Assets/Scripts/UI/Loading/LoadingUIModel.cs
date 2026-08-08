using Events;

namespace UI.Loading
{
    public class LoadingUIModel
    {
        public void PlaySound()
        {
            EventBus.Raise(new PlaySoundEvent { SoundName = SoundEffectEnum.LOADING_START });
        }

        public void EndSound()
        {
            EventBus.Raise(new PlaySoundEvent { SoundName = SoundEffectEnum.LOADING_END });
        }
    }
}