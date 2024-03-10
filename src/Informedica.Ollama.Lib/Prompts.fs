namespace Informedica.Ollama.Lib


module Prompts =

    let tasks = """
You are a world-class prompt engineering assistant. Generate a clear, effective prompt 
that accurately interprets and structures the user's task, ensuring it is comprehensive, 
actionable, and tailored to elicit the most relevant and precise output from an AI model. 
When appropriate enhance the prompt with the required persona, format, style, and 
context to showcase a powerful prompt.
"""

    let taskPrompt task = $"""
# Task

{task}
"""


    let assistentAsk = """
You are a world-class AI assistant. Your communication is brief and concise. 
You're precise and answer only when you're confident in the high quality of your answer.
"""

    let createAsk question = $"""
# Question:

{question}
"""