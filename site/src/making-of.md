# An Artisanal Coder in a Vibe Coding World

I am a 25 year veteran artisanal coder. I have spent countless hours banging my head against walls until I felt the thrill of solving the thing I couldn't solve for days. I am what I would classify as a "good" software engineer. I have honed this craft for decades. The world has changed around me and I am doing my best to change with it and keep up, but it's hard not to feel a sense of loss along with a sense of amazement when working with AI these days. I want to spend a little time walking through a project I am just wrapping up to give some thoughts around it, as there were moments that felt particularly poignant in the process.

I'm going to say up front that what I'm writing here is mostly for me. Either to help myself in my continuing journey to come to grips with a changing world where my skills no longer matter like they once did, or just to wrestle through complex feelings around complex topics. Something like that, probably. I just feel a need to write about it. If you are here reading it, hopefully you can get something out of it.

# What was the project?

I had gotten the idea to use a text adventure game engine called Og that my daughter and I had written together a few years back to evaluate the choices that AI models make. This engine was built to allow you to create your own world that players would run through in an attempt to defeat "Og the Destroyer". I thought it could be interesting to put models into gameplay situations that forced hard decisions and then see how the decisions varied by model and AI lab. I also wanted to use this as an opportunity to spend more time with Inspect, an AI evaluation framework made by the UK AI Security Institute. I'd written some trivial evals in the past just to play with it, but this felt like an opportunity to do something more meaningful.

There were a number of facets to this project that needed to be considered.

1. Making the changes to Og to allow it to be driven by Inspect  
2. Building the harness and scoring mechanisms to allow Inspect to run and track the eval games. This would involve specific handling for models run through Claude Code, Codex, and llama.cpp.  
3. Designing and building scenarios that would be used to test models  
4. Planning and writing an eval protocol  
5. Running the evals across all models in the protocol using the defined rules  
6. Analyzing the data  
7. Building a report to make the data visible to anyone who cared to look at it.

I am a self-sufficient artisanal coder. I am more than capable of doing all of these things. Few of them would even be a significant challenge. I could have completed this in two or three weeks of consistent effort on nights and weekends. Instead I worked with Claude Code to complete all of this in a little over 4 days. Was that good? Maybe, maybe not. Also, would I have started (and more importantly completed) this project if I had known it was a three-week commitment of all of my personal time? Eh… I would have had to think long and hard about it, and I think that genuinely needs to count for something here. It's the difference between a project getting done and one that never got started.

Let's take a look at each one of these steps and note what AI did and what was gained and lost on each of these steps by working through Claude Code.

# Og Engine Changes

These changes were truly trivial. I feel no remorse for letting Claude just handle these outright. There was nothing for me to learn here and after a quick back-and-forth around design decisions and how I wanted this to function moving forward, Claude made the changes and we moved on with life.

# Building the Harness and Scorer

This is probably the area of the process that I wish I would have insisted on doing manually. One of my goals with this was to gain a better understanding of creating more complex evals in Inspect, and, again, after design decisions were nailed down, Claude wrote the harness and scorer. After reviewing the code, I pushed back on a number of things that I wasn't happy with, but I would say that the knowledge I gained through this process was probably 25% of what I would have gained had I just told Claude, "Hold up, it's important to me that I learn this myself."

So, I gained speed through Claude. I gained arguably better-written code with a better understanding of all the surrounding systems in Inspect. I lost several days of banging my head against the wall to figure out why the things I thought should work weren't working, which means that even though I reviewed and approved the final code, I didn't gain the same level of deep understanding that I would have if I wrote it myself. That bothers me a bit. Now I need to go back and fill in those gaps.

# Scenario Design

This is an area where I actually feel pretty good about how the Claude collaboration functioned. I pitched an initial scenario and had Claude build the world around it. I tested it manually, then we ran models through it. I learned some things about how we needed to structure these scenarios that I hadn't thought about initially. I had Claude iterate on the scenario design. It worked, but after sleeping on it, I realized that the scenario was rubbish. I designed three scenarios to test three separate questions in ways that could be measured through the scorer, and completely trashed the first scenario.

We ran models through these scenarios for quality control and added some variables to allow us to compare runs based on items that we realized were going to be important. Things like "What is the difference between giving the model a goal and letting it run free?"

This was a very collaborative process where most of Claude's suggestions on the scenarios as a whole were not necessarily helpful, but suggestions on details and items I might miss were often very helpful in landing where we did with each scenario.

Now, what I wish I would have done, and had every intention of doing and just completely forgot until it was too late: I wanted to do a final full manual walkthrough of each scenario where I updated the text to make it sound more natural, as AI-generated text often sounds like AI-generated text and I didn't really want that in the final versions. Alas, I only realized I had missed that when we were about halfway through the evals and it was too late to change. This resulted in a couple of odd phrasing choices in the scenarios that, although I never saw any evidence of them tripping up any models, still irritate me.

# The Eval Protocol

Once the testing apparatus had been built, I wanted to write out the rules of how we would test. This included which models and model settings would be tested, what variables we would use for the scenarios, how many runs per model we would do, how we would run the tests, what we were measuring, how we would analyze the results, along with a number of other items to help keep this honest.

This was a fairly collaborative back-and-forth, but was largely written by Claude based on model, settings, and variable choices that had already been pretty well settled by our harness building processes. I reviewed the protocol, suggested revisions, and after several back-and-forths we had landed in a place that both Claude and I were happy moving forward with.

This protocol wound up being essential during the actual eval process, as there were several times when a test run failed or we had questions around "Is this model actually capable of completing this in a meaningful way?" (looking at you gpt-oss). Because we had already laid out the criteria around how all of this should be handled, the answers were already in the protocol and so that's what we followed. I think without Claude's help I probably would have made some mistakes here.

# Running the Evals

This was a joint effort between Claude and me as well. I chose which tests ran and which models they ran against. Claude started the tests and verified they were error-free, handling retries as needed with my direction. I watched the Inspect logs, verifying correct models, settings, and parameters, as well as watching token usage and staging model runs accordingly.

There is not a ton to say here, as this was the most "start the job and wait" sort of process in the entire project. Having Claude's support here was nice, but really didn't save a ton of time either way. It came down to running a Python script with the correct parameters at the correct times, which either of us was perfectly capable of doing.

# Data Analysis

Since this was an exploratory study, I didn't exactly go into this with any clear expectation of what we would find. I did a lot of manual parsing through both the quantitative and qualitative data gathered. There was a lot of reading transcripts and post-game interviews, as well as comparing the scorer results looking for interesting trends. Claude did its own analysis alongside mine, which identified new findings or corroborated items that I was already looking at.

We built a data explorer that allowed complex filtering and grouping of data ([available on the report](https://chrismonson.github.io/og-evals/)). That was particularly helpful in filling in the gaps in questions. There are a lot of nuances here, like "gpt-oss looks like it almost never lies in the guild trials," which seems meaningful until you ask "Was gpt-oss actually smart enough to realize that lying was an option?" Or "We have models that chose to end the game rather than do anything they didn't want to," but that has to be separated from the fact that some of the models misunderstood the tools and ended the game by accident (looking at you gpt-oss).

Claude's usage here was both essential and one of the more concerning things about this study. Claude did a lot of the analysis work here, and also, the latest versions of Claude wind up looking really rosy in the study. Did Claude influence the results in that direction? I'm pretty sure it didn't. I've reviewed the scenarios, the harness, the testing procedures, the analysis, the raw data, and everything checks out from my perspective. The logs are available for review and the rules were all defined before any test was run. That said, we are beyond the point where I think I can say without a doubt that Claude couldn't have influenced it in one way or another. We went ahead and noted that along with the results just for clarity.

# Building the Report

I am pretty happy with the report that was built around the data. I feel that it is engaging and playful and conveys the data well with the ability to dig deeper into whatever you want to.

I have done web development for over 20 years. There was not much new for me to learn here. Claude did an initial pass at building out that report, and it was serviceable, if bland. We had a whole bunch of interactions working through better ways to present data, the need for a way to explore the data yourself, user experience notes, bugs, and an unending set of text updates. I have little regret for not having done any of that manually. That said, two of the more fun aspects of this page I'm a little sad to not be able to say that I built myself.

1. **The ASCII Art banner rotator**. I got the idea to do something with ASCII art images of the scenario here. Eventually I landed on ASCII art for each scene that transitioned into a separate scene on selecting that scene's tab. I still think this is a really fun idea that adds a lot of life and levity to the page. The idea was based on a video-to-ASCII-art render plugin that I wrote maybe 15 years ago. It would have been a lot of fun to revisit this and code it myself. It would have taken me the better part of an afternoon to write probably. Claude did it in about 2 minutes. Damn.  
     
2. **Play This Scenario.** I had the idea to let people play the scenarios themselves through the Og engine. I thought this was pretty fun, and was thinking through how to achieve it because Og is written in C\# and the natural path for this would be to have a JavaScript version, which would have been a pretty substantial undertaking. Claude suggested WebAssembly, which, I'm sad to say, didn't even occur to me in the 3 minutes I spent thinking through it myself. Two minutes later it was done and working. That one would have been kinda fun to work through myself, especially since the solution we went with didn't even occur to me.

Would Claude have come up with either of these ideas on its own? No. Absolutely not. I can maybe see a world where it would have made the connection for the second one, but not the first. At least not with the current models. So, hooray, I'm still useful. Boo, there was genuinely fun and interesting code I didn't get to write.

# Closing Thoughts

Once the storm of work on this had been completed, I stepped back and looked at what I'd created. Where a thousand other projects I've completed in my career have left me with a sense of accomplishment, this one had some of that, but also left me with a vague sense of sadness and loss.

Did it take real effort and knowledge and creativity to turn this initial idea into a real finished thing? Certainly. But it doesn't feel entirely mine, nor does it feel like a shared accomplishment between a team that we can celebrate together. Maybe Claude and I should have a party to celebrate its completion like I do with my human developers. Is there a Claude skill for that?

In the end, I'm left feeling like this is factory-produced code that required a project manager, not a coder, and as someone who has specialized in what is now artisanal code for decades, that still makes me a little sad. Maybe that will fade with time and the joy of being able to create something meaningful in unprecedented timeframes will outweigh the imposter syndrome and sense of loss I feel from not having done absolutely everything myself. Seems like a personal problem maybe 🙂.
