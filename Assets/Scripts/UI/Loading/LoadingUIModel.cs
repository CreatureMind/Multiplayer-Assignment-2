using Events;

namespace UI.Loading
{
    public class LoadingUIModel
    {
        public void PlayStartSound()
        {
            EventBus.Raise(new PlaySoundEvent { SoundName = SoundEffectEnum.LOADING_START });
        }

        public void PlayEndSound()
        {
            EventBus.Raise(new PlaySoundEvent { SoundName = SoundEffectEnum.LOADING_END });
        }
    }
}