# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

-----

## [0.12.0] - 2026-06-26

### TTS Tools

#### Changed

- Modify TTSEngine so users may change the AudioSource to stream generated audio to use.
- Add a function to TTSEngine that just returns the generated audio as an Audioclip and does not play it in any source.
- TTS sample scene now allows switching between streaming audio and generating audioclips.

-----

## [0.11.2] - 2026-06-24

#### Fixed

- Compile errors in ChatEngine when LLM for Unity is not present.

-----

## [0.11.1] - 2026-06-24

#### Changed

- Confirmed compatability with Unity version 6.2.10f1.

#### Fixed

- Unity Mathmatics reference in RoleBot.TTS assembly. Turns out a direct reference is needed.
- Fixed crash caused when a response from the LLM was canceled before it has a chance to init.
- Fixed bug with BotController interrupt, where audio would continue to play even after the interrupt.

-----

## [0.11.0] - 2026-06-23

### Triggers and TTS Failsafe.

#### Added

- Triggers: Tools that can be used to shape how the chatbot acts
    - Parrot
        - Forces the chatbot to repeat a given phrase verbatim, and adds it to the chat history.
- Add trigger samples

#### Changed

- TTS can now pronounce words that aren't even in its dictionary because of a new failsafe in the graphene-to-phoneme pipeline.
    - This causes text with unknown words to take much longer to tokenize, so tokenization order is now strictly enforced in KokoroHandler.GenerateSpeech.
- Added OpenPhonemizer to the download manifest and 3rd party license sheet
- Added option to remove audio tags (coughing, blank audio, etc...) from STT.
- Add public methods in ChatEngine add a message from the llm or user to the chat history. 
- Add "stop response on interrupt" to textbot and speechbot samples
- Added average response time to textbot and speechbot samples.

-----

## [0.10.0] - 2026-06-07

### Adding the resource downloader.

#### Added

- A resource downloader, accessible through window -> RoleBot -> Resource Downloader
    - This will allow developers to easily download the AI models (and other various resources) that RoleBot needs to function!

#### Changed

- The TTS and STT engines no longer require public references to their respective AI models. The models are now loaded directly from the resources folder.

-----

## [0.9.0] - 2026-06-06

### This is the first release of *\<RoleBot\>*.

#### Added

- AI model inference for STT and TTS using Unity InferenceEngine and models downloaded from huggingface.
    - STT is using whisper-tiny, and TTS is using Kokoro.
- Chatbot capabilities using LLM for Unity.
- Engine scripts for each of the 3 stages (STT, LLM, and TTS) that'll act as user-friendly front end scripts to control each stage individually.
    - The STT engine comes with a very basic voice activity detection (VAD) algorithm, this can be disabled if desired.
- The beginnings of a "RoleBot" Engine which will act as a pipeline for the other engines, enabling easy to set up AI chatting.
- Sample scenes for each of the 4 engines.