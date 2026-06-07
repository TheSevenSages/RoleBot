# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
